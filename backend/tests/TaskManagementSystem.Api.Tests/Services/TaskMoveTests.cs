using Microsoft.EntityFrameworkCore;
using TaskManagementSystem.Api.Dtos.Tasks;
using TaskManagementSystem.Api.Models;
using TaskManagementSystem.Api.Services;
using TaskManagementSystem.Api.Tests.Infrastructure;

namespace TaskManagementSystem.Api.Tests.Services;

public class TaskMoveTests : IClassFixture<TestDatabaseFixture>
{
    private readonly TestDatabaseFixture _db;

    public TaskMoveTests(TestDatabaseFixture db)
    {
        _db = db;
    }

    [Fact(DisplayName = "M2 カード移動で status が変わり、状態履歴が記録される")]
    public async Task MoveAsync_ChangesStatusAndRecordsHistory()
    {
        await using var arrange = _db.CreateContext();
        var (project, owner) = await TestData.CreateProjectWithOwnerAsync(arrange);
        var task = await TestData.CreateTaskAsync(arrange, project.Id); // 既定 TODO

        await using var context = _db.CreateContext();
        var outcome = await new TaskService(context).MoveAsync(
            task.Id, new MoveTaskRequest("IN_PROGRESS", null), owner.Id);

        Assert.Equal(MoveTaskResult.Success, outcome.Result);
        Assert.Equal("IN_PROGRESS", outcome.Data!.Status);

        await using var verify = _db.CreateContext();
        var saved = await verify.Tasks.AsNoTracking().SingleAsync(t => t.Id == task.Id);
        Assert.Equal("IN_PROGRESS", saved.Status);

        var history = await verify.TaskStatusHistories.AsNoTracking()
            .Where(h => h.TaskId == task.Id).ToListAsync();
        Assert.Single(history);
        Assert.Equal("TODO", history[0].FromStatus);
        Assert.Equal("IN_PROGRESS", history[0].ToStatus);
        Assert.Equal(owner.Id, history[0].ChangedBy);
    }

    [Fact(DisplayName = "M2 ワークフロー外の移動先は InvalidStatus")]
    public async Task MoveAsync_RejectsUnknownStatus()
    {
        await using var arrange = _db.CreateContext();
        var (project, owner) = await TestData.CreateProjectWithOwnerAsync(arrange);
        var task = await TestData.CreateTaskAsync(arrange, project.Id);

        await using var context = _db.CreateContext();
        var outcome = await new TaskService(context).MoveAsync(
            task.Id, new MoveTaskRequest("NOPE", null), owner.Id);

        Assert.Equal(MoveTaskResult.InvalidStatus, outcome.Result);
    }

    [Fact(DisplayName = "M2 Viewer はカード移動できない(403 相当)")]
    public async Task MoveAsync_ForbiddenForViewer()
    {
        await using var arrange = _db.CreateContext();
        var (project, owner) = await TestData.CreateProjectWithOwnerAsync(arrange);
        var viewer = await TestData.CreateUserAsync(arrange);
        var workspaceId = await arrange.Projects
            .Where(p => p.Id == project.Id).Select(p => p.WorkspaceId).SingleAsync();
        await TestData.AddWorkspaceMemberAsync(arrange, workspaceId, viewer.Id, WorkspaceMemberRole.Viewer);
        var task = await TestData.CreateTaskAsync(arrange, project.Id);

        await using var context = _db.CreateContext();
        var outcome = await new TaskService(context).MoveAsync(
            task.Id, new MoveTaskRequest("DONE", null), viewer.Id);

        Assert.Equal(MoveTaskResult.Forbidden, outcome.Result);
    }

    [Fact(DisplayName = "M2 beforeTaskId 指定で列内の並び順が決まる")]
    public async Task MoveAsync_OrdersWithinColumn()
    {
        await using var arrange = _db.CreateContext();
        var (project, owner) = await TestData.CreateProjectWithOwnerAsync(arrange);
        var service = new TaskService(arrange);

        // すべて TODO 列に作成(作成時に末尾へ採番: 0,1,2)
        var a = (await service.CreateAsync(project.Id, Req("A"), owner.Id)).Data!;
        var b = (await service.CreateAsync(project.Id, Req("B"), owner.Id)).Data!;
        var c = (await service.CreateAsync(project.Id, Req("C"), owner.Id)).Data!;

        // C を A の直前へ移動 → 並びは C, A, B
        await service.MoveAsync(c.Id, new MoveTaskRequest("TODO", a.Id), owner.Id);

        var ordered = await arrange.Tasks.AsNoTracking()
            .Where(t => t.ProjectId == project.Id && t.Status == "TODO")
            .OrderBy(t => t.BoardPosition).ThenBy(t => t.Id)
            .Select(t => t.Title)
            .ToListAsync();

        Assert.Equal(new[] { c.Title, a.Title, b.Title }, ordered);
    }

    private static TaskRequest Req(string title) =>
        new(null, title, null, "TODO", "MEDIUM", null);
}
