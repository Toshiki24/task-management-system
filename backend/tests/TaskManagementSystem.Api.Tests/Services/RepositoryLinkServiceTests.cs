using Microsoft.EntityFrameworkCore;
using TaskManagementSystem.Api.Dtos.Git;
using TaskManagementSystem.Api.Models;
using TaskManagementSystem.Api.Services;
using TaskManagementSystem.Api.Tests.Infrastructure;

namespace TaskManagementSystem.Api.Tests.Services;

public class RepositoryLinkServiceTests : IClassFixture<TestDatabaseFixture>
{
    private readonly TestDatabaseFixture _db;

    public RepositoryLinkServiceTests(TestDatabaseFixture db)
    {
        _db = db;
    }

    private static async Task<GitConnection> AddConnectionAsync(
        TaskManagementSystem.Api.Data.AppDbContext ctx, long workspaceId, string account = "acme")
    {
        var conn = new GitConnection
        {
            WorkspaceId = workspaceId,
            Provider = GitProvider.GitHub,
            AuthType = GitAuthType.GitHubApp,
            ExternalAccount = account,
            Status = GitConnectionStatus.Active,
        };
        ctx.GitConnections.Add(conn);
        await ctx.SaveChangesAsync();
        return conn;
    }

    [Fact(DisplayName = "M4 連携の作成は OWNER/WS Admin のみ、一般メンバーは 403")]
    public async Task Create_RequiresManageProject()
    {
        await using var ctx = _db.CreateContext();
        var (project, owner) = await TestData.CreateProjectWithOwnerAsync(ctx);
        var member = await TestData.CreateUserAsync(ctx);
        await TestData.AddMemberAsync(ctx, project.Id, member.Id, ProjectMemberRole.Member);
        var conn = await AddConnectionAsync(ctx, project.WorkspaceId);
        var service = new RepositoryLinkService(ctx);
        var req = new CreateRepositoryLinkRequest(conn.Id, "r1", "acme/app", "main");

        Assert.Equal(RepositoryLinkResult.Forbidden, (await service.CreateAsync(project.Id, req, member.Id)).Result);
        var created = await service.CreateAsync(project.Id, req, owner.Id);
        Assert.Equal(RepositoryLinkResult.Success, created.Result);
        Assert.Equal("acme/app", created.Data!.RepoFullName);
    }

    [Fact(DisplayName = "M4 別ワークスペースの接続は InvalidConnection")]
    public async Task Create_ForeignConnection_Rejected()
    {
        await using var ctx = _db.CreateContext();
        var (project, owner) = await TestData.CreateProjectWithOwnerAsync(ctx);
        var otherWorkspace = await TestData.CreateWorkspaceAsync(ctx);
        var foreignConn = await AddConnectionAsync(ctx, otherWorkspace.Id, "other");
        var service = new RepositoryLinkService(ctx);

        Assert.Equal(RepositoryLinkResult.InvalidConnection,
            (await service.CreateAsync(project.Id,
                new CreateRepositoryLinkRequest(foreignConn.Id, "r1", "other/app", null), owner.Id)).Result);
    }

    [Fact(DisplayName = "M4 同じ接続・同じリポジトリの重複連携は Duplicate")]
    public async Task Create_Duplicate_Rejected()
    {
        await using var ctx = _db.CreateContext();
        var (project, owner) = await TestData.CreateProjectWithOwnerAsync(ctx);
        var conn = await AddConnectionAsync(ctx, project.WorkspaceId);
        var service = new RepositoryLinkService(ctx);
        var req = new CreateRepositoryLinkRequest(conn.Id, "r1", "acme/app", null);

        Assert.Equal(RepositoryLinkResult.Success, (await service.CreateAsync(project.Id, req, owner.Id)).Result);
        Assert.Equal(RepositoryLinkResult.Duplicate, (await service.CreateAsync(project.Id, req, owner.Id)).Result);
    }

    [Fact(DisplayName = "M4 連携の削除は OWNER/WS Admin のみ")]
    public async Task Delete_RequiresManageProject()
    {
        await using var ctx = _db.CreateContext();
        var (project, owner) = await TestData.CreateProjectWithOwnerAsync(ctx);
        var member = await TestData.CreateUserAsync(ctx);
        await TestData.AddMemberAsync(ctx, project.Id, member.Id, ProjectMemberRole.Member);
        var conn = await AddConnectionAsync(ctx, project.WorkspaceId);
        var service = new RepositoryLinkService(ctx);
        var link = (await service.CreateAsync(project.Id,
            new CreateRepositoryLinkRequest(conn.Id, "r1", "acme/app", null), owner.Id)).Data!;

        Assert.Equal(RepositoryLinkResult.Forbidden, await service.DeleteAsync(link.Id, member.Id));
        Assert.Equal(RepositoryLinkResult.Success, await service.DeleteAsync(link.Id, owner.Id));
        ctx.ChangeTracker.Clear();
        Assert.False(await ctx.RepositoryLinks.AnyAsync(r => r.Id == link.Id));
    }

    [Fact(DisplayName = "M4 非所属ユーザーには連携一覧を開示しない(null)")]
    public async Task List_NonMember_Null()
    {
        await using var ctx = _db.CreateContext();
        var (project, _) = await TestData.CreateProjectWithOwnerAsync(ctx);
        var outsider = await TestData.CreateUserAsync(ctx);

        Assert.Null(await new RepositoryLinkService(ctx).GetByProjectAsync(project.Id, outsider.Id));
    }
}
