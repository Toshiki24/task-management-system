using TaskManagementSystem.Api.Dtos.Git;
using TaskManagementSystem.Api.Models;
using TaskManagementSystem.Api.Services;
using TaskManagementSystem.Api.Tests.Infrastructure;

namespace TaskManagementSystem.Api.Tests.Services;

public class GitIdentityServiceTests : IClassFixture<TestDatabaseFixture>
{
    private readonly TestDatabaseFixture _db;

    public GitIdentityServiceTests(TestDatabaseFixture db)
    {
        _db = db;
    }

    private static CreateGitIdentityRequest Req(long userId, string externalId = "u-123") =>
        new(userId, GitProvider.GitHub, externalId, "octocat");

    [Fact(DisplayName = "M4 対応付けの作成・閲覧は WS Admin のみ、一般メンバーは不可")]
    public async Task Create_And_List_RequireAdmin()
    {
        await using var ctx = _db.CreateContext();
        var workspace = await TestData.CreateWorkspaceAsync(ctx);
        var admin = await TestData.CreateUserAsync(ctx);
        var member = await TestData.CreateUserAsync(ctx);
        await TestData.AddWorkspaceMemberAsync(ctx, workspace.Id, admin.Id, WorkspaceMemberRole.Admin);
        await TestData.AddWorkspaceMemberAsync(ctx, workspace.Id, member.Id, WorkspaceMemberRole.Member);
        var service = new GitIdentityService(ctx);

        Assert.Equal(GitIdentityResult.Forbidden, (await service.CreateAsync(workspace.Id, Req(member.Id), member.Id)).Result);

        var created = await service.CreateAsync(workspace.Id, Req(member.Id), admin.Id);
        Assert.Equal(GitIdentityResult.Success, created.Result);
        Assert.Equal(member.Id, created.Data!.UserId);
        Assert.Equal(member.Name, created.Data.UserName);

        // 一般メンバー・非所属には開示しない(Admin 限定)
        Assert.Null(await service.GetByWorkspaceAsync(workspace.Id, member.Id));
        Assert.Single((await service.GetByWorkspaceAsync(workspace.Id, admin.Id))!);
    }

    [Fact(DisplayName = "M4 ワークスペース非メンバーへの対応付けは UserNotMember")]
    public async Task Create_NonMember_Rejected()
    {
        await using var ctx = _db.CreateContext();
        var workspace = await TestData.CreateWorkspaceAsync(ctx);
        var admin = await TestData.CreateUserAsync(ctx);
        var outsider = await TestData.CreateUserAsync(ctx);
        await TestData.AddWorkspaceMemberAsync(ctx, workspace.Id, admin.Id, WorkspaceMemberRole.Admin);
        var service = new GitIdentityService(ctx);

        Assert.Equal(GitIdentityResult.UserNotMember,
            (await service.CreateAsync(workspace.Id, Req(outsider.Id), admin.Id)).Result);
    }

    [Fact(DisplayName = "M4 同一プロバイダ・外部ユーザーIDの重複は Duplicate")]
    public async Task Create_Duplicate_Rejected()
    {
        await using var ctx = _db.CreateContext();
        var workspace = await TestData.CreateWorkspaceAsync(ctx);
        var admin = await TestData.CreateUserAsync(ctx);
        await TestData.AddWorkspaceMemberAsync(ctx, workspace.Id, admin.Id, WorkspaceMemberRole.Admin);
        var service = new GitIdentityService(ctx);

        Assert.Equal(GitIdentityResult.Success, (await service.CreateAsync(workspace.Id, Req(admin.Id, "dup"), admin.Id)).Result);
        Assert.Equal(GitIdentityResult.Duplicate, (await service.CreateAsync(workspace.Id, Req(admin.Id, "dup"), admin.Id)).Result);
    }

    [Fact(DisplayName = "M4 actor 解決: 対応付けがあれば本人、無ければ null")]
    public async Task ResolveUserId()
    {
        await using var ctx = _db.CreateContext();
        var workspace = await TestData.CreateWorkspaceAsync(ctx);
        var admin = await TestData.CreateUserAsync(ctx);
        await TestData.AddWorkspaceMemberAsync(ctx, workspace.Id, admin.Id, WorkspaceMemberRole.Admin);
        var service = new GitIdentityService(ctx);
        await service.CreateAsync(workspace.Id, Req(admin.Id, "gh-777"), admin.Id);

        Assert.Equal(admin.Id, await service.ResolveUserIdAsync(workspace.Id, GitProvider.GitHub, "gh-777"));
        Assert.Null(await service.ResolveUserIdAsync(workspace.Id, GitProvider.GitHub, "unknown"));
        // 別プロバイダでは解決しない
        Assert.Null(await service.ResolveUserIdAsync(workspace.Id, GitProvider.GitLab, "gh-777"));
    }

    [Fact(DisplayName = "M4 削除は WS Admin のみ")]
    public async Task Delete_RequiresAdmin()
    {
        await using var ctx = _db.CreateContext();
        var workspace = await TestData.CreateWorkspaceAsync(ctx);
        var admin = await TestData.CreateUserAsync(ctx);
        var member = await TestData.CreateUserAsync(ctx);
        await TestData.AddWorkspaceMemberAsync(ctx, workspace.Id, admin.Id, WorkspaceMemberRole.Admin);
        await TestData.AddWorkspaceMemberAsync(ctx, workspace.Id, member.Id, WorkspaceMemberRole.Member);
        var service = new GitIdentityService(ctx);
        var created = (await service.CreateAsync(workspace.Id, Req(member.Id), admin.Id)).Data!;

        Assert.Equal(GitIdentityResult.Forbidden, await service.DeleteAsync(created.Id, member.Id));
        Assert.Equal(GitIdentityResult.Success, await service.DeleteAsync(created.Id, admin.Id));
    }

    [Fact(DisplayName = "M4 非所属ユーザーには一覧を開示しない(null)")]
    public async Task List_NonMember_Null()
    {
        await using var ctx = _db.CreateContext();
        var workspace = await TestData.CreateWorkspaceAsync(ctx);
        var outsider = await TestData.CreateUserAsync(ctx);

        Assert.Null(await new GitIdentityService(ctx).GetByWorkspaceAsync(workspace.Id, outsider.Id));
    }
}
