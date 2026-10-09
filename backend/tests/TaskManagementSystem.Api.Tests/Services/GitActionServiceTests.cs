using Microsoft.EntityFrameworkCore;
using TaskManagementSystem.Api.Dtos.Git;
using TaskManagementSystem.Api.Models;
using TaskManagementSystem.Api.Services;
using TaskManagementSystem.Api.Services.Git;
using TaskManagementSystem.Api.Tests.Infrastructure;

namespace TaskManagementSystem.Api.Tests.Services;

public class GitActionServiceTests : IClassFixture<TestDatabaseFixture>
{
    private readonly TestDatabaseFixture _db;

    public GitActionServiceTests(TestDatabaseFixture db)
    {
        _db = db;
    }

    private static GitActionService NewService(TaskManagementSystem.Api.Data.AppDbContext ctx)
    {
        var resolver = new GitProviderResolver(new IGitProvider[]
        {
            new FakeGitProvider(GitProvider.GitHub),
            new FakeGitProvider(GitProvider.GitLab),
        });
        return new GitActionService(ctx, resolver);
    }

    private static async Task<(RepositoryLink link, TaskItem task, User owner)> SetupAsync(
        TaskManagementSystem.Api.Data.AppDbContext ctx)
    {
        var (project, owner) = await TestData.CreateProjectWithOwnerAsync(ctx);
        var conn = new GitConnection
        {
            WorkspaceId = project.WorkspaceId, Provider = GitProvider.GitHub, AuthType = GitAuthType.GitHubApp,
            ExternalAccount = TestData.Unique("acme"), Status = GitConnectionStatus.Active,
        };
        ctx.GitConnections.Add(conn);
        await ctx.SaveChangesAsync();
        var link = new RepositoryLink
        {
            ProjectId = project.Id, GitConnectionId = conn.Id,
            ExternalRepoId = TestData.Unique("r"), RepoFullName = "acme/app", DefaultBranch = "main",
        };
        ctx.RepositoryLinks.Add(link);
        var task = new TaskItem { ProjectId = project.Id, Title = "Fix login bug", Status = "TODO" };
        ctx.Tasks.Add(task);
        await ctx.SaveChangesAsync();
        return (link, task, owner);
    }

    [Fact(DisplayName = "M4 ブランチ作成でリンクが記録され、既定のブランチ名を採番する")]
    public async Task CreateBranch_RecordsLink()
    {
        await using var ctx = _db.CreateContext();
        var (link, task, owner) = await SetupAsync(ctx);
        var service = NewService(ctx);

        var outcome = await service.CreateBranchAsync(task.Id, new CreateBranchRequest(link.Id, null, null), owner.Id);

        Assert.Equal(GitActionResult.Success, outcome.Result);
        Assert.Equal(GitLinkType.Branch, outcome.Data!.LinkType);
        Assert.Equal($"feature/{task.Id}-fix-login-bug", outcome.Data.ExternalRef);
        Assert.True(await ctx.TaskGitLinks.AnyAsync(l => l.TaskId == task.Id && l.LinkType == GitLinkType.Branch));
        Assert.True(await ctx.Activities.AnyAsync(a => a.TaskId == task.Id && a.Verb == ActivityVerb.GitBranchCreated));
    }

    [Fact(DisplayName = "M4 PR 作成で PR リンク(OPEN)が記録される")]
    public async Task CreatePullRequest_RecordsLink()
    {
        await using var ctx = _db.CreateContext();
        var (link, task, owner) = await SetupAsync(ctx);
        var service = NewService(ctx);

        var outcome = await service.CreatePullRequestAsync(
            task.Id, new CreatePullRequestRequest(link.Id, "feature/x", null, null), owner.Id);

        Assert.Equal(GitActionResult.Success, outcome.Result);
        Assert.Equal(GitLinkType.PullRequest, outcome.Data!.LinkType);
        Assert.Equal(GitLinkState.Open, outcome.Data.State);
    }

    [Fact(DisplayName = "M4 Viewer はブランチ作成できない(Forbidden)")]
    public async Task CreateBranch_Viewer_Forbidden()
    {
        await using var ctx = _db.CreateContext();
        var (link, task, _) = await SetupAsync(ctx);
        var viewer = await TestData.CreateUserAsync(ctx);
        var workspaceId = await ctx.Projects.Where(p => p.Id == task.ProjectId).Select(p => p.WorkspaceId).FirstAsync();
        await TestData.AddWorkspaceMemberAsync(ctx, workspaceId, viewer.Id, WorkspaceMemberRole.Viewer);
        var service = NewService(ctx);

        var outcome = await service.CreateBranchAsync(task.Id, new CreateBranchRequest(link.Id, null, null), viewer.Id);
        Assert.Equal(GitActionResult.Forbidden, outcome.Result);
    }

    [Fact(DisplayName = "M4 別プロジェクトの連携リポジトリは InvalidRepositoryLink")]
    public async Task CreateBranch_ForeignRepo_Invalid()
    {
        await using var ctx = _db.CreateContext();
        var (_, task, owner) = await SetupAsync(ctx);
        // 別プロジェクトの連携
        var (otherLink, _, _) = await SetupAsync(ctx);
        var service = NewService(ctx);

        var outcome = await service.CreateBranchAsync(task.Id, new CreateBranchRequest(otherLink.Id, null, null), owner.Id);
        Assert.Equal(GitActionResult.InvalidRepositoryLink, outcome.Result);
    }

    [Fact(DisplayName = "M4 非所属ユーザーには TaskNotFound(存在を開示しない)")]
    public async Task CreateBranch_NonMember_NotFound()
    {
        await using var ctx = _db.CreateContext();
        var (link, task, _) = await SetupAsync(ctx);
        var outsider = await TestData.CreateUserAsync(ctx);
        var service = NewService(ctx);

        var outcome = await service.CreateBranchAsync(task.Id, new CreateBranchRequest(link.Id, null, null), outsider.Id);
        Assert.Equal(GitActionResult.TaskNotFound, outcome.Result);
    }
}
