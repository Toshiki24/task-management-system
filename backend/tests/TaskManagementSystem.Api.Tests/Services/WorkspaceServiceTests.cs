using TaskManagementSystem.Api.Dtos.Workspaces;
using TaskManagementSystem.Api.Models;
using TaskManagementSystem.Api.Services;
using TaskManagementSystem.Api.Tests.Infrastructure;

namespace TaskManagementSystem.Api.Tests.Services;

public class WorkspaceServiceTests : IClassFixture<TestDatabaseFixture>
{
    private readonly TestDatabaseFixture _db;

    public WorkspaceServiceTests(TestDatabaseFixture db)
    {
        _db = db;
    }

    private static WorkspaceRequest NewRequest() => new(TestData.Unique("ws"), "説明");

    private async Task<User> CreateSystemAdminAsync()
    {
        await using var context = _db.CreateContext();
        var user = await TestData.CreateUserAsync(context);
        user.IsSystemAdmin = true;
        await context.SaveChangesAsync();
        return user;
    }

    [Fact(DisplayName = "ワークスペース作成は System Admin のみ可能")]
    public async Task CreateAsync_OnlySystemAdmin()
    {
        var admin = await CreateSystemAdminAsync();
        await using var nonAdminCtx = _db.CreateContext();
        var nonAdmin = await TestData.CreateUserAsync(nonAdminCtx);

        await using var ctx1 = _db.CreateContext();
        var forbidden = await new WorkspaceService(ctx1).CreateAsync(NewRequest(), nonAdmin.Id);
        Assert.Equal(CreateWorkspaceResult.Forbidden, forbidden.Result);

        await using var ctx2 = _db.CreateContext();
        var created = await new WorkspaceService(ctx2).CreateAsync(NewRequest(), admin.Id);
        Assert.Equal(CreateWorkspaceResult.Success, created.Result);
        Assert.NotNull(created.Data);
        Assert.False(created.Data!.IsArchived);
    }

    [Fact(DisplayName = "全件一覧は System Admin 以外は null(= 403)")]
    public async Task GetAllAsync_NullForNonSystemAdmin()
    {
        var admin = await CreateSystemAdminAsync();
        await using var ctx = _db.CreateContext();
        var member = await TestData.CreateUserAsync(ctx);

        Assert.Null(await new WorkspaceService(ctx).GetAllAsync(member.Id));
        Assert.NotNull(await new WorkspaceService(ctx).GetAllAsync(admin.Id));
    }

    [Fact(DisplayName = "GetMine は自分が所属するワークスペースのみ返す")]
    public async Task GetMineAsync_ReturnsOnlyOwnMemberships()
    {
        await using var ctx = _db.CreateContext();
        var user = await TestData.CreateUserAsync(ctx);
        var mine = await TestData.CreateWorkspaceAsync(ctx);
        await TestData.AddWorkspaceMemberAsync(ctx, mine.Id, user.Id, WorkspaceMemberRole.Member);
        var other = await TestData.CreateWorkspaceAsync(ctx);

        var result = await new WorkspaceService(ctx).GetMineAsync(user.Id);

        Assert.Contains(result, w => w.Id == mine.Id && w.MyRole == WorkspaceMemberRole.Member);
        Assert.DoesNotContain(result, w => w.Id == other.Id);
    }

    [Fact(DisplayName = "非所属ユーザーは詳細取得で null(存在を開示しない)")]
    public async Task GetByIdAsync_NullForNonMember()
    {
        await using var ctx = _db.CreateContext();
        var outsider = await TestData.CreateUserAsync(ctx);
        var workspace = await TestData.CreateWorkspaceAsync(ctx);

        Assert.Null(await new WorkspaceService(ctx).GetByIdAsync(workspace.Id, outsider.Id));
    }

    [Fact(DisplayName = "System Admin は非所属でも詳細取得でき MyRole は null")]
    public async Task GetByIdAsync_SystemAdminSeesWithoutMembership()
    {
        var admin = await CreateSystemAdminAsync();
        await using var ctx = _db.CreateContext();
        var workspace = await TestData.CreateWorkspaceAsync(ctx);

        var dto = await new WorkspaceService(ctx).GetByIdAsync(workspace.Id, admin.Id);

        Assert.NotNull(dto);
        Assert.Null(dto!.MyRole);
    }

    [Fact(DisplayName = "更新は WS Admin のみ。Member は 403、非所属は 404")]
    public async Task UpdateAsync_PermissionMatrix()
    {
        await using var ctx = _db.CreateContext();
        var wsAdmin = await TestData.CreateUserAsync(ctx);
        var wsMember = await TestData.CreateUserAsync(ctx);
        var outsider = await TestData.CreateUserAsync(ctx);
        var workspace = await TestData.CreateWorkspaceAsync(ctx);
        await TestData.AddWorkspaceMemberAsync(ctx, workspace.Id, wsAdmin.Id, WorkspaceMemberRole.Admin);
        await TestData.AddWorkspaceMemberAsync(ctx, workspace.Id, wsMember.Id, WorkspaceMemberRole.Member);

        Assert.Equal(UpdateWorkspaceResult.NotFound,
            (await new WorkspaceService(ctx).UpdateAsync(workspace.Id, NewRequest(), outsider.Id)).Result);
        Assert.Equal(UpdateWorkspaceResult.Forbidden,
            (await new WorkspaceService(ctx).UpdateAsync(workspace.Id, NewRequest(), wsMember.Id)).Result);

        var updated = await new WorkspaceService(ctx).UpdateAsync(
            workspace.Id, new WorkspaceRequest("新しい名前", "新説明"), wsAdmin.Id);
        Assert.Equal(UpdateWorkspaceResult.Success, updated.Result);
        Assert.Equal("新しい名前", updated.Data!.Name);
    }

    [Fact(DisplayName = "アーカイブは WS Admin のみ。成功で IsArchived=true")]
    public async Task ArchiveAsync_SetsArchived()
    {
        await using var ctx = _db.CreateContext();
        var wsAdmin = await TestData.CreateUserAsync(ctx);
        var wsMember = await TestData.CreateUserAsync(ctx);
        var workspace = await TestData.CreateWorkspaceAsync(ctx);
        await TestData.AddWorkspaceMemberAsync(ctx, workspace.Id, wsAdmin.Id, WorkspaceMemberRole.Admin);
        await TestData.AddWorkspaceMemberAsync(ctx, workspace.Id, wsMember.Id, WorkspaceMemberRole.Member);

        Assert.Equal(ArchiveWorkspaceResult.Forbidden,
            await new WorkspaceService(ctx).ArchiveAsync(workspace.Id, wsMember.Id));

        Assert.Equal(ArchiveWorkspaceResult.Success,
            await new WorkspaceService(ctx).ArchiveAsync(workspace.Id, wsAdmin.Id));

        var dto = await new WorkspaceService(ctx).GetByIdAsync(workspace.Id, wsAdmin.Id);
        Assert.True(dto!.IsArchived);
    }
}
