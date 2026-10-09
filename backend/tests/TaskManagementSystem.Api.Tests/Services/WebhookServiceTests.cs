using Microsoft.EntityFrameworkCore;
using TaskManagementSystem.Api.Models;
using TaskManagementSystem.Api.Services;
using TaskManagementSystem.Api.Services.Git;
using TaskManagementSystem.Api.Tests.Infrastructure;

namespace TaskManagementSystem.Api.Tests.Services;

public class WebhookServiceTests : IClassFixture<TestDatabaseFixture>
{
    private readonly TestDatabaseFixture _db;

    public WebhookServiceTests(TestDatabaseFixture db)
    {
        _db = db;
    }

    private const string Secret = "wh-secret";

    private static WebhookService NewService(TaskManagementSystem.Api.Data.AppDbContext ctx)
    {
        var resolver = new GitProviderResolver(new IGitProvider[]
        {
            new FakeGitProvider(GitProvider.GitHub),
            new FakeGitProvider(GitProvider.GitLab),
        });
        var gitLinks = new GitLinkService(ctx, new GitIdentityService(ctx));
        return new WebhookService(ctx, resolver, new DevWebhookSecretResolver(), gitLinks);
    }

    /// <summary>
    /// 指定の externalRepoId で連携済みリポジトリを持つ接続を用意する。テスト DB は共有のため
    /// リポジトリ ID は毎回ユニークにして、他テストの連携と衝突しないようにする。
    /// </summary>
    private static async Task<GitConnection> SetupLinkedRepoAsync(
        TaskManagementSystem.Api.Data.AppDbContext ctx, string repoId)
    {
        var (project, _) = await TestData.CreateProjectWithOwnerAsync(ctx);
        var conn = new GitConnection
        {
            WorkspaceId = project.WorkspaceId,
            Provider = GitProvider.GitHub,
            AuthType = GitAuthType.GitHubApp,
            ExternalAccount = TestData.Unique("acme"),
            SecretRef = Secret,
            Status = GitConnectionStatus.Active,
        };
        ctx.GitConnections.Add(conn);
        await ctx.SaveChangesAsync();
        ctx.RepositoryLinks.Add(new RepositoryLink
        {
            ProjectId = project.Id,
            GitConnectionId = conn.Id,
            ExternalRepoId = repoId,
            RepoFullName = "acme/app",
        });
        await ctx.SaveChangesAsync();
        return conn;
    }

    private static string Body(string repoId, string type = "pr_merged") =>
        $"{{\"type\":\"{type}\",\"repoId\":\"{repoId}\",\"repoFullName\":\"acme/app\",\"ref\":\"feature/1-x\",\"title\":\"Closes #1\"}}";

    private static GitWebhookRequest Request(string body, string? signature, string? deliveryId)
    {
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (signature is not null) headers["x-signature"] = signature;
        if (deliveryId is not null) headers["x-delivery-id"] = deliveryId;
        return new GitWebhookRequest(headers, body);
    }

    [Fact(DisplayName = "M4 署名検証OK: 連携リポジトリの Webhook を記録する(verified)")]
    public async Task Accepted_RecordsVerified()
    {
        await using var ctx = _db.CreateContext();
        var repoId = TestData.Unique("repo");
        var conn = await SetupLinkedRepoAsync(ctx, repoId);
        var body = Body(repoId);
        var sig = FakeGitProvider.ComputeSignature(body, Secret);
        var service = NewService(ctx);

        var outcome = await service.IngestAsync(GitProvider.GitHub, Request(body, sig, "d-1"));

        Assert.Equal(WebhookIngestResult.Accepted, outcome.Result);
        var ev = await ctx.WebhookEvents.SingleAsync(e => e.GitConnectionId == conn.Id);
        Assert.True(ev.SignatureVerified);
        // 取り込み処理まで完了するため PROCESSED(M4 §7)
        Assert.Equal(WebhookEventStatus.Processed, ev.Status);
        Assert.Equal("d-1", ev.ExternalEventId);
    }

    [Fact(DisplayName = "M4 冪等: 同一配信IDの再送はスキップし、記録は1件")]
    public async Task Duplicate_Skipped()
    {
        await using var ctx = _db.CreateContext();
        var repoId = TestData.Unique("repo");
        var conn = await SetupLinkedRepoAsync(ctx, repoId);
        var body = Body(repoId);
        var sig = FakeGitProvider.ComputeSignature(body, Secret);
        var service = NewService(ctx);

        Assert.Equal(WebhookIngestResult.Accepted, (await service.IngestAsync(GitProvider.GitHub, Request(body, sig, "d-1"))).Result);
        Assert.Equal(WebhookIngestResult.Duplicate, (await service.IngestAsync(GitProvider.GitHub, Request(body, sig, "d-1"))).Result);

        Assert.Equal(1, await ctx.WebhookEvents.CountAsync(e => e.GitConnectionId == conn.Id));
    }

    [Fact(DisplayName = "M4 署名NG: InvalidSignature を返し、FAILED として記録する")]
    public async Task InvalidSignature_RecordedFailed()
    {
        await using var ctx = _db.CreateContext();
        var repoId = TestData.Unique("repo");
        var conn = await SetupLinkedRepoAsync(ctx, repoId);
        var service = NewService(ctx);

        var outcome = await service.IngestAsync(GitProvider.GitHub, Request(Body(repoId), "deadbeef", "d-2"));

        Assert.Equal(WebhookIngestResult.InvalidSignature, outcome.Result);
        var ev = await ctx.WebhookEvents.SingleAsync(e => e.GitConnectionId == conn.Id);
        Assert.False(ev.SignatureVerified);
        Assert.Equal(WebhookEventStatus.Failed, ev.Status);
    }

    [Fact(DisplayName = "M4 連携していないリポジトリは Ignored(記録しない)")]
    public async Task UnlinkedRepo_Ignored()
    {
        await using var ctx = _db.CreateContext();
        await SetupLinkedRepoAsync(ctx, TestData.Unique("repo"));
        var unlinkedBody = Body(TestData.Unique("unlinked"));
        var sig = FakeGitProvider.ComputeSignature(unlinkedBody, Secret);
        var deliveryId = TestData.Unique("d");
        var service = NewService(ctx);

        var outcome = await service.IngestAsync(GitProvider.GitHub, Request(unlinkedBody, sig, deliveryId));

        Assert.Equal(WebhookIngestResult.Ignored, outcome.Result);
        Assert.Equal(0, await ctx.WebhookEvents.CountAsync(e => e.ExternalEventId == deliveryId));
    }

    [Fact(DisplayName = "M4 配信ID無し・未知プロバイダはそれぞれ専用の結果を返す")]
    public async Task MissingDelivery_And_UnknownProvider()
    {
        await using var ctx = _db.CreateContext();
        var repoId = TestData.Unique("repo");
        await SetupLinkedRepoAsync(ctx, repoId);
        var body = Body(repoId);
        var sig = FakeGitProvider.ComputeSignature(body, Secret);
        var service = NewService(ctx);

        Assert.Equal(WebhookIngestResult.MissingDeliveryId,
            (await service.IngestAsync(GitProvider.GitHub, Request(body, sig, null))).Result);
        Assert.Equal(WebhookIngestResult.UnknownProvider,
            (await service.IngestAsync("BITBUCKET", Request(body, sig, TestData.Unique("d")))).Result);
    }
}
