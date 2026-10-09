using System.Security.Cryptography;
using System.Text;
using TaskManagementSystem.Api.Services.Git;

namespace TaskManagementSystem.Api.Tests.Services;

/// <summary>
/// 実アダプタ(GitHub/GitLab)の Webhook 面(署名検証・配信ID・イベント正規化)と SSRF ガードの単体テスト。
/// 書き込み(REST)は外部ネットワークに出るため対象外(本番専用)。
/// </summary>
public class GitProviderAdaptersTests
{
    private static GitWebhookRequest Req(string body, params (string, string)[] headers)
    {
        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (k, v) in headers) dict[k] = v;
        return new GitWebhookRequest(dict, body);
    }

    // IHttpClientFactory / ISecretStore は Webhook 面では使わないので簡易スタブ
    private sealed class NullHttpFactory : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new();
    }

    private sealed class NullSecretStore : ISecretStore
    {
        public Task<string?> GetSecretAsync(string? secretRef, CancellationToken ct = default) =>
            Task.FromResult<string?>(secretRef);
    }

    private static GitHubProvider GitHub() => new(new NullHttpFactory(), new NullSecretStore());
    private static GitLabProvider GitLab() => new(new NullHttpFactory(), new NullSecretStore());

    // --- GitHub ---

    [Fact(DisplayName = "M4 GitHub 署名: X-Hub-Signature-256(HMAC-SHA256)を検証する")]
    public void GitHub_VerifySignature()
    {
        const string secret = "whsec";
        var body = "{\"zen\":\"x\"}";
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var sig = "sha256=" + Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(body))).ToLowerInvariant();

        Assert.True(GitHub().VerifySignature(Req(body, ("x-hub-signature-256", sig)), secret));
        Assert.False(GitHub().VerifySignature(Req(body, ("x-hub-signature-256", "sha256=bad")), secret));
        Assert.False(GitHub().VerifySignature(Req(body), secret));
    }

    [Fact(DisplayName = "M4 GitHub イベント: pull_request closed+merged を PrMerged に正規化する")]
    public void GitHub_ParsePullRequestMerged()
    {
        var body = """
        {"action":"closed","number":42,
         "repository":{"id":99,"full_name":"acme/app"},
         "sender":{"id":7,"login":"octocat"},
         "pull_request":{"merged":true,"title":"Closes #12","body":"fix","html_url":"https://x/pull/42","head":{"ref":"feature/12-x"}}}
        """;
        var ev = GitHub().ParseEvent(Req(body, ("x-github-event", "pull_request"), ("x-github-delivery", "d1")));

        Assert.NotNull(ev);
        Assert.Equal(GitEventType.PrMerged, ev!.Type);
        Assert.Equal("acme/app", ev.Repo.FullName);
        Assert.Equal("42", ev.Ref);
        Assert.Equal("octocat", ev.Actor!.Username);
        Assert.Contains("12", ev.TaskHints);
        Assert.Equal("d1", GitHub().GetDeliveryId(Req(body, ("x-github-delivery", "d1"))));
    }

    [Fact(DisplayName = "M4 GitHub イベント: create(branch)を BranchCreated に正規化する")]
    public void GitHub_ParseCreateBranch()
    {
        var body = """
        {"ref_type":"branch","ref":"feature/TASK-5-login","repository":{"id":1,"full_name":"o/r"},"sender":{"id":2,"login":"u"}}
        """;
        var ev = GitHub().ParseEvent(Req(body, ("x-github-event", "create")));

        Assert.Equal(GitEventType.BranchCreated, ev!.Type);
        Assert.Equal("feature/TASK-5-login", ev.Ref);
        // ブランチ名に #id / TASK-id があれば参照として拾う
        Assert.Contains("5", ev.TaskHints);
    }

    // --- GitLab ---

    [Fact(DisplayName = "M4 GitLab 署名: X-Gitlab-Token の一致で検証する")]
    public void GitLab_VerifySignature()
    {
        Assert.True(GitLab().VerifySignature(Req("{}", ("x-gitlab-token", "tok")), "tok"));
        Assert.False(GitLab().VerifySignature(Req("{}", ("x-gitlab-token", "nope")), "tok"));
        Assert.False(GitLab().VerifySignature(Req("{}"), "tok"));
    }

    [Fact(DisplayName = "M4 GitLab イベント: merge_request merge を MrMerged に正規化する")]
    public void GitLab_ParseMergeRequestMerged()
    {
        var body = """
        {"object_kind":"merge_request","project":{"id":55,"path_with_namespace":"grp/app"},
         "user":{"id":3,"username":"dev"},
         "object_attributes":{"action":"merge","iid":7,"title":"Closes #34","description":"d","source_branch":"feature/34","state":"merged","url":"https://x/mr/7"}}
        """;
        var ev = GitLab().ParseEvent(Req(body, ("x-gitlab-event", "Merge Request Hook"), ("x-gitlab-event-uuid", "u1")));

        Assert.Equal(GitEventType.MrMerged, ev!.Type);
        Assert.Equal("grp/app", ev.Repo.FullName);
        Assert.Equal("7", ev.Ref);
        Assert.Equal("dev", ev.Actor!.Username);
        Assert.Contains("34", ev.TaskHints);
        Assert.Equal("u1", GitLab().GetDeliveryId(Req(body, ("x-gitlab-event-uuid", "u1"))));
    }

    [Fact(DisplayName = "M4 GitLab イベント: push(before=0..0)を BranchCreated に正規化する")]
    public void GitLab_ParsePushBranchCreate()
    {
        var body = """
        {"object_kind":"push","before":"0000000000000000000000000000000000000000",
         "ref":"refs/heads/feature/9-x","project":{"id":1,"path_with_namespace":"g/p"},
         "user_id":2,"user_username":"u","commits":[{"message":"work on #9"}]}
        """;
        var ev = GitLab().ParseEvent(Req(body, ("x-gitlab-event", "Push Hook")));

        Assert.Equal(GitEventType.BranchCreated, ev!.Type);
        Assert.Equal("feature/9-x", ev.Ref);
        Assert.Contains("9", ev.TaskHints);
    }

    // --- SSRF ---

    [Theory(DisplayName = "M4 SSRF: 外部 https ホストは許可、内部/非 https は拒否")]
    [InlineData("https://gitlab.com", true)]
    [InlineData("https://gitlab.example.com", true)]
    [InlineData("http://gitlab.example.com", false)]       // http は不可
    [InlineData("https://localhost", false)]
    [InlineData("https://127.0.0.1", false)]
    [InlineData("https://10.0.0.5", false)]
    [InlineData("https://192.168.1.10", false)]
    [InlineData("https://169.254.169.254", false)]          // メタデータ
    [InlineData("https://git.internal", false)]
    [InlineData("https://user:pass@gitlab.com", false)]     // 資格情報入り
    [InlineData("not-a-url", false)]
    public void SsrfGuard_Validates(string url, bool expected)
    {
        Assert.Equal(expected, SsrfGuard.IsAllowedBaseUrl(url));
    }
}
