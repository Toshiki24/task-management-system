using TaskManagementSystem.Api.Dtos.Workflow;
using TaskManagementSystem.Api.Models;
using TaskManagementSystem.Api.Services;
using TaskManagementSystem.Api.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace TaskManagementSystem.Api.Tests.Services;

public class WorkflowStateServiceTests : IClassFixture<TestDatabaseFixture>
{
    private readonly TestDatabaseFixture _db;

    public WorkflowStateServiceTests(TestDatabaseFixture db)
    {
        _db = db;
    }

    [Fact(DisplayName = "新規WSには既定の3状態(TODO/IN_PROGRESS/DONE)が用意される")]
    public async Task NewWorkspace_HasDefaultStates()
    {
        await using var ctx = _db.CreateContext();
        var member = await TestData.CreateUserAsync(ctx);
        var ws = await TestData.CreateWorkspaceAsync(ctx);
        await TestData.AddWorkspaceMemberAsync(ctx, ws.Id, member.Id, WorkspaceMemberRole.Member);

        var states = await new WorkflowStateService(ctx).GetByWorkspaceAsync(ws.Id, member.Id);

        Assert.NotNull(states);
        Assert.Equal(new[] { "TODO", "IN_PROGRESS", "DONE" }, states!.Select(s => s.Key));
        Assert.True(states.Single(s => s.Key == "TODO").IsDefault);
    }

    [Fact(DisplayName = "状態一覧は非所属ユーザーには null(= 404)")]
    public async Task GetByWorkspace_NullForNonMember()
    {
        await using var ctx = _db.CreateContext();
        var outsider = await TestData.CreateUserAsync(ctx);
        var ws = await TestData.CreateWorkspaceAsync(ctx);

        Assert.Null(await new WorkflowStateService(ctx).GetByWorkspaceAsync(ws.Id, outsider.Id));
    }

    [Fact(DisplayName = "状態の追加は WS Admin のみ。Member は 403")]
    public async Task Create_OnlyAdmin()
    {
        await using var ctx = _db.CreateContext();
        var member = await TestData.CreateUserAsync(ctx);
        var ws = await TestData.CreateWorkspaceAsync(ctx);
        await TestData.AddWorkspaceMemberAsync(ctx, ws.Id, member.Id, WorkspaceMemberRole.Member);

        var result = await new WorkflowStateService(ctx).CreateAsync(
            ws.Id, new WorkflowStateCreateRequest("REVIEW", "レビュー中", WorkflowStateCategory.InProgress, null), member.Id);

        Assert.Equal(CreateWorkflowStateResult.Forbidden, result.Result);
    }

    [Fact(DisplayName = "状態を追加すると末尾に並ぶ。重複キーは 409")]
    public async Task Create_AppendsAndRejectsDuplicate()
    {
        await using var ctx = _db.CreateContext();
        var admin = await TestData.CreateUserAsync(ctx);
        var ws = await TestData.CreateWorkspaceAsync(ctx);
        await TestData.AddWorkspaceMemberAsync(ctx, ws.Id, admin.Id, WorkspaceMemberRole.Admin);
        var service = new WorkflowStateService(ctx);

        var created = await service.CreateAsync(
            ws.Id, new WorkflowStateCreateRequest("REVIEW", "レビュー中", WorkflowStateCategory.InProgress, "#abc"), admin.Id);

        Assert.Equal(CreateWorkflowStateResult.Success, created.Result);
        Assert.Equal(3, created.Data!.Position); // 既定3状態(0,1,2)の次
        Assert.False(created.Data.IsDefault);

        var duplicate = await service.CreateAsync(
            ws.Id, new WorkflowStateCreateRequest("REVIEW", "別名", WorkflowStateCategory.Todo, null), admin.Id);
        Assert.Equal(CreateWorkflowStateResult.DuplicateKey, duplicate.Result);
    }

    [Fact(DisplayName = "並び替え(Position指定)で順序が入れ替わり 0..n に振り直される")]
    public async Task Update_Reorders()
    {
        await using var ctx = _db.CreateContext();
        var admin = await TestData.CreateUserAsync(ctx);
        var ws = await TestData.CreateWorkspaceAsync(ctx);
        await TestData.AddWorkspaceMemberAsync(ctx, ws.Id, admin.Id, WorkspaceMemberRole.Admin);
        var service = new WorkflowStateService(ctx);

        var done = (await service.GetByWorkspaceAsync(ws.Id, admin.Id))!.Single(s => s.Key == "DONE");

        // DONE を先頭(0)へ移動
        await service.UpdateAsync(
            ws.Id, done.Id, new WorkflowStateUpdateRequest("完了", WorkflowStateCategory.Done, 0, null), admin.Id);

        var ordered = (await service.GetByWorkspaceAsync(ws.Id, admin.Id))!.Select(s => s.Key).ToArray();
        Assert.Equal(new[] { "DONE", "TODO", "IN_PROGRESS" }, ordered);
    }

    [Fact(DisplayName = "既定状態は削除できない(409 相当)")]
    public async Task Delete_DefaultBlocked()
    {
        await using var ctx = _db.CreateContext();
        var admin = await TestData.CreateUserAsync(ctx);
        var ws = await TestData.CreateWorkspaceAsync(ctx);
        await TestData.AddWorkspaceMemberAsync(ctx, ws.Id, admin.Id, WorkspaceMemberRole.Admin);
        var service = new WorkflowStateService(ctx);

        var todo = (await service.GetByWorkspaceAsync(ws.Id, admin.Id))!.Single(s => s.Key == "TODO");

        Assert.Equal(DeleteWorkflowStateResult.DefaultState,
            await service.DeleteAsync(ws.Id, todo.Id, null, admin.Id));
    }

    [Fact(DisplayName = "使用中の状態は moveTo 必須。付け替え先を指定すればタスクを移してから削除する")]
    public async Task Delete_InUseRequiresMoveTo()
    {
        await using var ctx = _db.CreateContext();
        var admin = await TestData.CreateUserAsync(ctx);
        var ws = await TestData.CreateWorkspaceAsync(ctx);
        await TestData.AddWorkspaceMemberAsync(ctx, ws.Id, admin.Id, WorkspaceMemberRole.Admin);
        var project = await TestData.CreateProjectAsync(ctx, workspaceId: ws.Id);
        var service = new WorkflowStateService(ctx);

        var review = (await service.CreateAsync(
            ws.Id, new WorkflowStateCreateRequest("REVIEW", "レビュー中", WorkflowStateCategory.InProgress, null), admin.Id)).Data!;

        // REVIEW を使うタスクを作る
        var task = await TestData.CreateTaskAsync(ctx, project.Id);
        task.Status = "REVIEW";
        await ctx.SaveChangesAsync();

        // 付け替え先なしでは削除できない
        Assert.Equal(DeleteWorkflowStateResult.InUse,
            await service.DeleteAsync(ws.Id, review.Id, null, admin.Id));

        // 付け替え先を指定すると、タスクを移してから削除する
        Assert.Equal(DeleteWorkflowStateResult.Success,
            await service.DeleteAsync(ws.Id, review.Id, "IN_PROGRESS", admin.Id));

        var moved = await ctx.Tasks.AsNoTracking().SingleAsync(t => t.Id == task.Id);
        Assert.Equal("IN_PROGRESS", moved.Status);
        Assert.False(await ctx.WorkflowStates.AnyAsync(s => s.WorkspaceId == ws.Id && s.Key == "REVIEW"));
    }
}
