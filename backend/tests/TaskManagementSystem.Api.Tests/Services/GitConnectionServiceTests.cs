using Microsoft.EntityFrameworkCore;
using TaskManagementSystem.Api.Dtos.Git;
using TaskManagementSystem.Api.Models;
using TaskManagementSystem.Api.Services;
using TaskManagementSystem.Api.Tests.Infrastructure;

namespace TaskManagementSystem.Api.Tests.Services;

public class GitConnectionServiceTests : IClassFixture<TestDatabaseFixture>
{
    private readonly TestDatabaseFixture _db;

    public GitConnectionServiceTests(TestDatabaseFixture db)
    {
        _db = db;
    }

    private static CreateGitConnectionRequest CreateReq(
        string provider = GitProvider.GitHub, string account = "acme") =>
        new(provider, null, GitAuthType.GitHubApp, "secret-ref/abc", account);

    [Fact(DisplayName = "M4 接続の作成は WS Admin のみ、一覧はメンバーも参照可(資格情報は非露出)")]
    public async Task Create_RequiresAdmin_List_AllowsMember()
    {
        await using var ctx = _db.CreateContext();
        var workspace = await TestData.CreateWorkspaceAsync(ctx);
        var admin = await TestData.CreateUserAsync(ctx);
        var member = await TestData.CreateUserAsync(ctx);
        await TestData.AddWorkspaceMemberAsync(ctx, workspace.Id, admin.Id, WorkspaceMemberRole.Admin);
        await TestData.AddWorkspaceMemberAsync(ctx, workspace.Id, member.Id, WorkspaceMemberRole.Member);
        var service = new GitConnectionService(ctx);

        // 作成はメンバー不可、Admin は可
        Assert.Equal(GitConnectionResult.Forbidden,
            (await service.CreateAsync(workspace.Id, CreateReq(), member.Id)).Result);

        var created = await service.CreateAsync(workspace.Id, CreateReq(), admin.Id);
        Assert.Equal(GitConnectionResult.Success, created.Result);
        Assert.Equal(GitProvider.GitHub, created.Data!.Provider);

        // 一覧はメンバーも参照できる(リポジトリ連携で接続を選ぶため)
        Assert.Single((await service.GetByWorkspaceAsync(workspace.Id, member.Id))!);
        Assert.Single((await service.GetByWorkspaceAsync(workspace.Id, admin.Id))!);
    }

    [Fact(DisplayName = "M4 資格情報(secret_ref)はレスポンスに含めないが DB には保存される")]
    public async Task SecretRef_NotExposed_ButPersisted()
    {
        await using var ctx = _db.CreateContext();
        var workspace = await TestData.CreateWorkspaceAsync(ctx);
        var admin = await TestData.CreateUserAsync(ctx);
        await TestData.AddWorkspaceMemberAsync(ctx, workspace.Id, admin.Id, WorkspaceMemberRole.Admin);
        var service = new GitConnectionService(ctx);

        var created = (await service.CreateAsync(workspace.Id, CreateReq(), admin.Id)).Data!;

        // DTO は record のため、secret を表す公開プロパティが存在しないことを型で担保
        Assert.DoesNotContain(typeof(GitConnectionDto).GetProperties(),
            p => p.Name.Contains("Secret", StringComparison.OrdinalIgnoreCase));
        ctx.ChangeTracker.Clear();
        Assert.Equal("secret-ref/abc", (await ctx.GitConnections.FindAsync(created.Id))!.SecretRef);
    }

    [Fact(DisplayName = "M4 同一プロバイダ・アカウントの重複接続は Duplicate")]
    public async Task Duplicate_Rejected()
    {
        await using var ctx = _db.CreateContext();
        var workspace = await TestData.CreateWorkspaceAsync(ctx);
        var admin = await TestData.CreateUserAsync(ctx);
        await TestData.AddWorkspaceMemberAsync(ctx, workspace.Id, admin.Id, WorkspaceMemberRole.Admin);
        var service = new GitConnectionService(ctx);

        Assert.Equal(GitConnectionResult.Success, (await service.CreateAsync(workspace.Id, CreateReq(), admin.Id)).Result);
        Assert.Equal(GitConnectionResult.Duplicate, (await service.CreateAsync(workspace.Id, CreateReq(), admin.Id)).Result);
    }

    [Fact(DisplayName = "M4 更新: secret_ref 未指定なら従来値を維持し、他項目は更新される")]
    public async Task Update_KeepsSecretWhenEmpty()
    {
        await using var ctx = _db.CreateContext();
        var workspace = await TestData.CreateWorkspaceAsync(ctx);
        var admin = await TestData.CreateUserAsync(ctx);
        await TestData.AddWorkspaceMemberAsync(ctx, workspace.Id, admin.Id, WorkspaceMemberRole.Admin);
        var service = new GitConnectionService(ctx);
        var created = (await service.CreateAsync(workspace.Id, CreateReq(), admin.Id)).Data!;

        var updated = await service.UpdateAsync(created.Id,
            new UpdateGitConnectionRequest(null, GitAuthType.Pat, null, "acme", GitConnectionStatus.Disabled), admin.Id);
        Assert.Equal(GitConnectionResult.Success, updated.Result);
        Assert.Equal(GitAuthType.Pat, updated.Data!.AuthType);
        Assert.Equal(GitConnectionStatus.Disabled, updated.Data.Status);

        ctx.ChangeTracker.Clear();
        Assert.Equal("secret-ref/abc", (await ctx.GitConnections.FindAsync(created.Id))!.SecretRef);
    }

    [Fact(DisplayName = "M4 削除で連携リポジトリも連動削除される(CASCADE)")]
    public async Task Delete_CascadesRepositoryLinks()
    {
        long connectionId;
        long linkId;
        await using (var ctx = _db.CreateContext())
        {
            var (project, owner) = await TestData.CreateProjectWithOwnerAsync(ctx);
            var workspaceId = project.WorkspaceId;
            var connService = new GitConnectionService(ctx);
            var conn = (await connService.CreateAsync(workspaceId, CreateReq(), owner.Id)).Data!;
            var linkService = new RepositoryLinkService(ctx);
            var link = (await linkService.CreateAsync(project.Id,
                new CreateRepositoryLinkRequest(conn.Id, "r1", "acme/app", "main"), owner.Id)).Data!;

            Assert.Equal(GitConnectionResult.Success, await connService.DeleteAsync(conn.Id, owner.Id));
            connectionId = conn.Id;
            linkId = link.Id;
        }

        await using (var ctx = _db.CreateContext())
        {
            Assert.False(await ctx.GitConnections.AnyAsync(c => c.Id == connectionId));
            Assert.False(await ctx.RepositoryLinks.AnyAsync(r => r.Id == linkId));
        }
    }

    [Fact(DisplayName = "M4 非所属ユーザーには一覧を開示しない(null)")]
    public async Task List_NonMember_Null()
    {
        await using var ctx = _db.CreateContext();
        var workspace = await TestData.CreateWorkspaceAsync(ctx);
        var outsider = await TestData.CreateUserAsync(ctx);

        Assert.Null(await new GitConnectionService(ctx).GetByWorkspaceAsync(workspace.Id, outsider.Id));
    }
}
