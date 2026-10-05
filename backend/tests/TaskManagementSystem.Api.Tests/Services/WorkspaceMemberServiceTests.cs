using TaskManagementSystem.Api.Dtos.Workspaces;
using TaskManagementSystem.Api.Models;
using TaskManagementSystem.Api.Services;
using TaskManagementSystem.Api.Tests.Infrastructure;

namespace TaskManagementSystem.Api.Tests.Services;

public class WorkspaceMemberServiceTests : IClassFixture<TestDatabaseFixture>
{
    private readonly TestDatabaseFixture _db;

    public WorkspaceMemberServiceTests(TestDatabaseFixture db)
    {
        _db = db;
    }

    [Fact(DisplayName = "メンバー一覧は非所属ユーザーには null(= 404)")]
    public async Task GetMembersAsync_NullForNonMember()
    {
        await using var ctx = _db.CreateContext();
        var outsider = await TestData.CreateUserAsync(ctx);
        var workspace = await TestData.CreateWorkspaceAsync(ctx);

        Assert.Null(await new WorkspaceMemberService(ctx).GetMembersAsync(workspace.Id, outsider.Id));
    }

    [Fact(DisplayName = "メンバー追加は WS Admin のみ。Member は 403")]
    public async Task AddMemberAsync_OnlyAdmin()
    {
        await using var ctx = _db.CreateContext();
        var admin = await TestData.CreateUserAsync(ctx);
        var member = await TestData.CreateUserAsync(ctx);
        var target = await TestData.CreateUserAsync(ctx);
        var workspace = await TestData.CreateWorkspaceAsync(ctx);
        await TestData.AddWorkspaceMemberAsync(ctx, workspace.Id, admin.Id, WorkspaceMemberRole.Admin);
        await TestData.AddWorkspaceMemberAsync(ctx, workspace.Id, member.Id, WorkspaceMemberRole.Member);

        Assert.Equal(AddWorkspaceMemberResult.Forbidden,
            (await new WorkspaceMemberService(ctx).AddMemberAsync(
                workspace.Id, new AddWorkspaceMemberRequest(target.Id, WorkspaceMemberRole.Member), member.Id)).Result);

        var added = await new WorkspaceMemberService(ctx).AddMemberAsync(
            workspace.Id, new AddWorkspaceMemberRequest(target.Id, WorkspaceMemberRole.Viewer), admin.Id);
        Assert.Equal(AddWorkspaceMemberResult.Success, added.Result);
        Assert.Equal(WorkspaceMemberRole.Viewer, added.Data!.Role);
    }

    [Fact(DisplayName = "存在しないユーザー追加は UserNotFound、重複は AlreadyMember")]
    public async Task AddMemberAsync_UserNotFoundAndDuplicate()
    {
        await using var ctx = _db.CreateContext();
        var admin = await TestData.CreateUserAsync(ctx);
        var workspace = await TestData.CreateWorkspaceAsync(ctx);
        await TestData.AddWorkspaceMemberAsync(ctx, workspace.Id, admin.Id, WorkspaceMemberRole.Admin);

        Assert.Equal(AddWorkspaceMemberResult.UserNotFound,
            (await new WorkspaceMemberService(ctx).AddMemberAsync(
                workspace.Id, new AddWorkspaceMemberRequest(TestData.NonExistentId, WorkspaceMemberRole.Member), admin.Id)).Result);

        Assert.Equal(AddWorkspaceMemberResult.AlreadyMember,
            (await new WorkspaceMemberService(ctx).AddMemberAsync(
                workspace.Id, new AddWorkspaceMemberRequest(admin.Id, WorkspaceMemberRole.Member), admin.Id)).Result);
    }

    [Fact(DisplayName = "非所属ワークスペースへの追加は WorkspaceNotFound(存在を開示しない)")]
    public async Task AddMemberAsync_WorkspaceNotFoundForNonMember()
    {
        await using var ctx = _db.CreateContext();
        var outsider = await TestData.CreateUserAsync(ctx);
        var target = await TestData.CreateUserAsync(ctx);
        var workspace = await TestData.CreateWorkspaceAsync(ctx);

        Assert.Equal(AddWorkspaceMemberResult.WorkspaceNotFound,
            (await new WorkspaceMemberService(ctx).AddMemberAsync(
                workspace.Id, new AddWorkspaceMemberRequest(target.Id, WorkspaceMemberRole.Member), outsider.Id)).Result);
    }

    [Fact(DisplayName = "ロール変更は WS Admin のみ。最後の ADMIN の降格は LastAdmin")]
    public async Task UpdateRoleAsync_LastAdminGuard()
    {
        await using var ctx = _db.CreateContext();
        var admin = await TestData.CreateUserAsync(ctx);
        var member = await TestData.CreateUserAsync(ctx);
        var workspace = await TestData.CreateWorkspaceAsync(ctx);
        await TestData.AddWorkspaceMemberAsync(ctx, workspace.Id, admin.Id, WorkspaceMemberRole.Admin);
        await TestData.AddWorkspaceMemberAsync(ctx, workspace.Id, member.Id, WorkspaceMemberRole.Member);

        // 唯一の ADMIN を降格 → ブロック
        Assert.Equal(UpdateWorkspaceMemberResult.LastAdmin,
            await new WorkspaceMemberService(ctx).UpdateRoleAsync(
                workspace.Id, admin.Id, new UpdateWorkspaceMemberRoleRequest(WorkspaceMemberRole.Member), admin.Id));

        // Member を昇格 → 成功
        Assert.Equal(UpdateWorkspaceMemberResult.Success,
            await new WorkspaceMemberService(ctx).UpdateRoleAsync(
                workspace.Id, member.Id, new UpdateWorkspaceMemberRoleRequest(WorkspaceMemberRole.Admin), admin.Id));

        // ADMIN が2人になったので、元の ADMIN の降格が可能に
        Assert.Equal(UpdateWorkspaceMemberResult.Success,
            await new WorkspaceMemberService(ctx).UpdateRoleAsync(
                workspace.Id, admin.Id, new UpdateWorkspaceMemberRoleRequest(WorkspaceMemberRole.Viewer), admin.Id));
    }

    [Fact(DisplayName = "メンバー削除は WS Admin のみ。最後の ADMIN の削除は LastAdmin")]
    public async Task RemoveMemberAsync_LastAdminGuard()
    {
        await using var ctx = _db.CreateContext();
        var admin = await TestData.CreateUserAsync(ctx);
        var member = await TestData.CreateUserAsync(ctx);
        var workspace = await TestData.CreateWorkspaceAsync(ctx);
        await TestData.AddWorkspaceMemberAsync(ctx, workspace.Id, admin.Id, WorkspaceMemberRole.Admin);
        await TestData.AddWorkspaceMemberAsync(ctx, workspace.Id, member.Id, WorkspaceMemberRole.Member);

        // Member 自身には削除権限がない
        Assert.Equal(RemoveWorkspaceMemberResult.Forbidden,
            await new WorkspaceMemberService(ctx).RemoveMemberAsync(workspace.Id, admin.Id, member.Id));

        // 唯一の ADMIN は削除できない
        Assert.Equal(RemoveWorkspaceMemberResult.LastAdmin,
            await new WorkspaceMemberService(ctx).RemoveMemberAsync(workspace.Id, admin.Id, admin.Id));

        // Member は削除できる
        Assert.Equal(RemoveWorkspaceMemberResult.Success,
            await new WorkspaceMemberService(ctx).RemoveMemberAsync(workspace.Id, member.Id, admin.Id));
    }
}
