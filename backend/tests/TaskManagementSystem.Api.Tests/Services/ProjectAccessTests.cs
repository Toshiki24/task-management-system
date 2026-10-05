using Microsoft.EntityFrameworkCore;
using TaskManagementSystem.Api.Models;
using TaskManagementSystem.Api.Services;
using TaskManagementSystem.Api.Tests.Infrastructure;

namespace TaskManagementSystem.Api.Tests.Services;

/// <summary>
/// 認可の集約(ResolveAccess)と可視性フィルタの単体テスト(Phase 2 M1 §3)。
/// 3 スコープ(System / Workspace / Project)の判定と、所属外ワークスペースの非開示を検証する。
/// </summary>
public class ProjectAccessTests : IClassFixture<TestDatabaseFixture>
{
    private readonly TestDatabaseFixture _db;

    public ProjectAccessTests(TestDatabaseFixture db)
    {
        _db = db;
    }

    [Fact(DisplayName = "存在しないプロジェクトは null(存在を開示しない)")]
    public async Task ResolveAccess_ReturnsNull_WhenProjectDoesNotExist()
    {
        await using var context = _db.CreateContext();
        var user = await TestData.CreateUserAsync(context);

        var result = await context.ResolveAccessAsync(TestData.NonExistentId, user.Id);

        Assert.Null(result);
    }

    [Fact(DisplayName = "所属していないワークスペースのプロジェクトは CanView=false")]
    public async Task ResolveAccess_NotVisible_WhenUserIsNotWorkspaceMember()
    {
        await using var context = _db.CreateContext();
        var outsider = await TestData.CreateUserAsync(context);
        var workspace = await TestData.CreateWorkspaceAsync(context);
        var project = await TestData.CreateProjectAsync(context, workspaceId: workspace.Id);

        var result = await context.ResolveAccessAsync(project.Id, outsider.Id);

        Assert.NotNull(result);
        Assert.False(result!.Value.CanView);
        Assert.False(result.Value.IsSystemAdmin);
        Assert.Null(result.Value.WorkspaceRole);
        Assert.Null(result.Value.ProjectRole);
    }

    [Fact(DisplayName = "WS Member は閲覧・書き込み可、WS Admin ではない")]
    public async Task ResolveAccess_WorkspaceMember_CanViewAndWrite()
    {
        await using var context = _db.CreateContext();
        var user = await TestData.CreateUserAsync(context);
        var workspace = await TestData.CreateWorkspaceAsync(context);
        await TestData.AddWorkspaceMemberAsync(context, workspace.Id, user.Id, WorkspaceMemberRole.Member);
        var project = await TestData.CreateProjectAsync(context, workspaceId: workspace.Id);

        var result = (await context.ResolveAccessAsync(project.Id, user.Id))!.Value;

        Assert.Equal(WorkspaceMemberRole.Member, result.WorkspaceRole);
        Assert.True(result.CanView);
        Assert.True(result.CanWrite);
        Assert.False(result.IsWorkspaceAdmin);
    }

    [Fact(DisplayName = "WS Viewer は閲覧のみ(書き込み不可)")]
    public async Task ResolveAccess_WorkspaceViewer_IsReadOnly()
    {
        await using var context = _db.CreateContext();
        var user = await TestData.CreateUserAsync(context);
        var workspace = await TestData.CreateWorkspaceAsync(context);
        await TestData.AddWorkspaceMemberAsync(context, workspace.Id, user.Id, WorkspaceMemberRole.Viewer);
        var project = await TestData.CreateProjectAsync(context, workspaceId: workspace.Id);

        var result = (await context.ResolveAccessAsync(project.Id, user.Id))!.Value;

        Assert.True(result.CanView);
        Assert.False(result.CanWrite);
        Assert.False(result.IsWorkspaceAdmin);
    }

    [Fact(DisplayName = "WS Admin は管理操作が可能")]
    public async Task ResolveAccess_WorkspaceAdmin_IsWorkspaceAdmin()
    {
        await using var context = _db.CreateContext();
        var user = await TestData.CreateUserAsync(context);
        var workspace = await TestData.CreateWorkspaceAsync(context);
        await TestData.AddWorkspaceMemberAsync(context, workspace.Id, user.Id, WorkspaceMemberRole.Admin);
        var project = await TestData.CreateProjectAsync(context, workspaceId: workspace.Id);

        var result = (await context.ResolveAccessAsync(project.Id, user.Id))!.Value;

        Assert.True(result.IsWorkspaceAdmin);
        Assert.True(result.CanWrite);
    }

    [Fact(DisplayName = "System Admin は非所属ワークスペースも横断して閲覧・書き込み可")]
    public async Task ResolveAccess_SystemAdmin_CanAccessAcrossWorkspaces()
    {
        await using var context = _db.CreateContext();
        var admin = await TestData.CreateUserAsync(context);
        admin.IsSystemAdmin = true;
        await context.SaveChangesAsync();

        var workspace = await TestData.CreateWorkspaceAsync(context);
        var project = await TestData.CreateProjectAsync(context, workspaceId: workspace.Id);

        var result = (await context.ResolveAccessAsync(project.Id, admin.Id))!.Value;

        Assert.True(result.IsSystemAdmin);
        Assert.True(result.CanView);
        Assert.True(result.CanWrite);
        Assert.True(result.IsWorkspaceAdmin);
        Assert.Null(result.WorkspaceRole); // 所属はしていないが System Admin として横断できる
    }

    [Fact(DisplayName = "プロジェクトロールとワークスペースロールが同時に解決される")]
    public async Task ResolveAccess_ResolvesBothWorkspaceAndProjectRoles()
    {
        await using var context = _db.CreateContext();
        var user = await TestData.CreateUserAsync(context);
        var workspace = await TestData.CreateWorkspaceAsync(context);
        await TestData.AddWorkspaceMemberAsync(context, workspace.Id, user.Id, WorkspaceMemberRole.Member);
        var project = await TestData.CreateProjectAsync(context, workspaceId: workspace.Id);
        await TestData.AddMemberAsync(context, project.Id, user.Id, ProjectMemberRole.Owner);

        var result = (await context.ResolveAccessAsync(project.Id, user.Id))!.Value;

        Assert.Equal(WorkspaceMemberRole.Member, result.WorkspaceRole);
        Assert.Equal(ProjectMemberRole.Owner, result.ProjectRole);
    }

    [Fact(DisplayName = "ResolveTaskAccess はタスク→プロジェクト→ワークスペースで解決する")]
    public async Task ResolveTaskAccess_ResolvesViaTask()
    {
        await using var context = _db.CreateContext();
        var user = await TestData.CreateUserAsync(context);
        var workspace = await TestData.CreateWorkspaceAsync(context);
        await TestData.AddWorkspaceMemberAsync(context, workspace.Id, user.Id, WorkspaceMemberRole.Member);
        var project = await TestData.CreateProjectAsync(context, workspaceId: workspace.Id);
        var task = await TestData.CreateTaskAsync(context, project.Id);

        var result = (await context.ResolveTaskAccessAsync(task.Id, user.Id))!.Value;

        Assert.True(result.CanView);
        Assert.Equal(WorkspaceMemberRole.Member, result.WorkspaceRole);
    }

    [Fact(DisplayName = "可視性フィルタ: 所属WSのプロジェクトのみ返り、他WSは除外される")]
    public async Task WhereVisibleTo_ReturnsOnlyProjectsInUserWorkspaces()
    {
        await using var context = _db.CreateContext();
        var user = await TestData.CreateUserAsync(context);

        var ownWorkspace = await TestData.CreateWorkspaceAsync(context);
        await TestData.AddWorkspaceMemberAsync(context, ownWorkspace.Id, user.Id, WorkspaceMemberRole.Member);
        var ownProject = await TestData.CreateProjectAsync(context, workspaceId: ownWorkspace.Id);

        var otherWorkspace = await TestData.CreateWorkspaceAsync(context);
        var otherProject = await TestData.CreateProjectAsync(context, workspaceId: otherWorkspace.Id);

        var visibleIds = await context.Projects
            .WhereVisibleTo(context, user.Id)
            .Select(p => p.Id)
            .ToListAsync();

        Assert.Contains(ownProject.Id, visibleIds);
        Assert.DoesNotContain(otherProject.Id, visibleIds);
    }

    [Fact(DisplayName = "可視性フィルタ: System Admin は全ワークスペースのプロジェクトが見える")]
    public async Task WhereVisibleTo_SystemAdmin_SeesAllProjects()
    {
        await using var context = _db.CreateContext();
        var admin = await TestData.CreateUserAsync(context);
        admin.IsSystemAdmin = true;
        await context.SaveChangesAsync();

        var workspace = await TestData.CreateWorkspaceAsync(context);
        var project = await TestData.CreateProjectAsync(context, workspaceId: workspace.Id);

        var visibleIds = await context.Projects
            .WhereVisibleTo(context, admin.Id)
            .Select(p => p.Id)
            .ToListAsync();

        Assert.Contains(project.Id, visibleIds);
    }
}
