using Microsoft.EntityFrameworkCore;
using TaskManagementSystem.Api.Models;
using TaskManagementSystem.Api.Services;
using TaskManagementSystem.Api.Services.Git;
using TaskManagementSystem.Api.Tests.Infrastructure;

namespace TaskManagementSystem.Api.Tests.Services;

public class GitLinkServiceTests : IClassFixture<TestDatabaseFixture>
{
    private readonly TestDatabaseFixture _db;

    public GitLinkServiceTests(TestDatabaseFixture db)
    {
        _db = db;
    }

    private static GitLinkService NewService(TaskManagementSystem.Api.Data.AppDbContext ctx) =>
        new(ctx, new GitIdentityService(ctx));

    private static async Task<(long repoLinkId, long projectId, long workspaceId, TaskItem task, User owner)>
        SetupAsync(TaskManagementSystem.Api.Data.AppDbContext ctx)
    {
        var (project, owner) = await TestData.CreateProjectWithOwnerAsync(ctx);
        var conn = new GitConnection
        {
            WorkspaceId = project.WorkspaceId,
            Provider = GitProvider.GitHub,
            AuthType = GitAuthType.GitHubApp,
            ExternalAccount = TestData.Unique("acme"),
            Status = GitConnectionStatus.Active,
        };
        ctx.GitConnections.Add(conn);
        await ctx.SaveChangesAsync();
        var repoLink = new RepositoryLink
        {
            ProjectId = project.Id, GitConnectionId = conn.Id,
            ExternalRepoId = TestData.Unique("r"), RepoFullName = "acme/app",
        };
        ctx.RepositoryLinks.Add(repoLink);
        var task = new TaskItem { ProjectId = project.Id, Title = TestData.Unique("task"), Status = "TODO" };
        ctx.Tasks.Add(task);
        await ctx.SaveChangesAsync();
        return (repoLink.Id, project.Id, project.WorkspaceId, task, owner);
    }

    private static GitEvent Event(GitEventType type, string refValue, IReadOnlyList<string> hints, GitActor? actor = null) =>
        new(type, new RepositoryRef("r", "acme/app"), actor, refValue, "Closes #X",
            "https://example.test/pr/1", DateTimeOffset.UtcNow, hints);

    [Fact(DisplayName = "M4 PR マージイベントで task_git_links を作成する(参照したタスクに紐づく)")]
    public async Task ApplyEvent_CreatesLink()
    {
        await using var ctx = _db.CreateContext();
        var (repoLinkId, projectId, workspaceId, task, _) = await SetupAsync(ctx);
        var service = NewService(ctx);

        var count = await service.ApplyEventAsync(
            new GitLinkContext(repoLinkId, projectId, workspaceId, GitProvider.GitHub),
            Event(GitEventType.PrMerged, "42", new[] { task.Id.ToString() }));

        Assert.Equal(1, count);
        var link = await ctx.TaskGitLinks.SingleAsync(l => l.TaskId == task.Id);
        Assert.Equal(GitLinkType.PullRequest, link.LinkType);
        Assert.Equal(GitLinkState.Merged, link.State);
        Assert.Equal("42", link.ExternalRef);
    }

    [Fact(DisplayName = "M4 同じ PR の状態変化は既存リンクを更新する(OPEN→MERGED)")]
    public async Task ApplyEvent_UpdatesState()
    {
        await using var ctx = _db.CreateContext();
        var (repoLinkId, projectId, workspaceId, task, _) = await SetupAsync(ctx);
        var service = NewService(ctx);
        var ctxArg = new GitLinkContext(repoLinkId, projectId, workspaceId, GitProvider.GitHub);

        await service.ApplyEventAsync(ctxArg, Event(GitEventType.PrOpened, "42", new[] { task.Id.ToString() }));
        await service.ApplyEventAsync(ctxArg, Event(GitEventType.PrMerged, "42", new[] { task.Id.ToString() }));

        var link = await ctx.TaskGitLinks.SingleAsync(l => l.TaskId == task.Id);
        Assert.Equal(GitLinkState.Merged, link.State);
    }

    [Fact(DisplayName = "M4 別プロジェクトのタスク参照は無視する")]
    public async Task ApplyEvent_IgnoresForeignTask()
    {
        await using var ctx = _db.CreateContext();
        var (repoLinkId, projectId, workspaceId, _, _) = await SetupAsync(ctx);
        var (otherProject, otherOwner) = await TestData.CreateProjectWithOwnerAsync(ctx);
        var foreignTask = new TaskItem { ProjectId = otherProject.Id, Title = "別PJ", Status = "TODO" };
        ctx.Tasks.Add(foreignTask);
        await ctx.SaveChangesAsync();
        var service = NewService(ctx);

        var count = await service.ApplyEventAsync(
            new GitLinkContext(repoLinkId, projectId, workspaceId, GitProvider.GitHub),
            Event(GitEventType.PrMerged, "42", new[] { foreignTask.Id.ToString() }));

        Assert.Equal(0, count);
    }

    [Fact(DisplayName = "M4 actor が identity マッピング済みならアクティビティを記録する")]
    public async Task ApplyEvent_RecordsActivityWhenActorMapped()
    {
        await using var ctx = _db.CreateContext();
        var (repoLinkId, projectId, workspaceId, task, owner) = await SetupAsync(ctx);
        // owner の Git identity を登録
        ctx.GitIdentities.Add(new GitIdentity
        {
            WorkspaceId = workspaceId, UserId = owner.Id, Provider = GitProvider.GitHub, ExternalUserId = "gh-1",
        });
        await ctx.SaveChangesAsync();
        var service = NewService(ctx);

        await service.ApplyEventAsync(
            new GitLinkContext(repoLinkId, projectId, workspaceId, GitProvider.GitHub),
            Event(GitEventType.PrMerged, "42", new[] { task.Id.ToString() }, new GitActor("gh-1", "octocat")));

        var activity = await ctx.Activities.SingleAsync(a => a.TaskId == task.Id && a.Verb == ActivityVerb.GitPrMerged);
        Assert.Equal(owner.Id, activity.ActorUserId);
    }

    [Fact(DisplayName = "M4 actor 未マッピングならリンクは作るがアクティビティは記録しない")]
    public async Task ApplyEvent_NoActivityWhenActorUnmapped()
    {
        await using var ctx = _db.CreateContext();
        var (repoLinkId, projectId, workspaceId, task, _) = await SetupAsync(ctx);
        var service = NewService(ctx);

        await service.ApplyEventAsync(
            new GitLinkContext(repoLinkId, projectId, workspaceId, GitProvider.GitHub),
            Event(GitEventType.PrMerged, "42", new[] { task.Id.ToString() }, new GitActor("unmapped", "ghost")));

        Assert.True(await ctx.TaskGitLinks.AnyAsync(l => l.TaskId == task.Id));
        Assert.False(await ctx.Activities.AnyAsync(a => a.TaskId == task.Id && a.Verb == ActivityVerb.GitPrMerged));
    }

    [Fact(DisplayName = "M4 リンク一覧は CanView、非所属は null")]
    public async Task GetByTask_Authz()
    {
        await using var ctx = _db.CreateContext();
        var (repoLinkId, projectId, workspaceId, task, owner) = await SetupAsync(ctx);
        var outsider = await TestData.CreateUserAsync(ctx);
        var service = NewService(ctx);
        await service.ApplyEventAsync(
            new GitLinkContext(repoLinkId, projectId, workspaceId, GitProvider.GitHub),
            Event(GitEventType.PrMerged, "42", new[] { task.Id.ToString() }));

        Assert.Single((await service.GetByTaskAsync(task.Id, owner.Id))!);
        Assert.Null(await service.GetByTaskAsync(task.Id, outsider.Id));
    }
}
