using Microsoft.EntityFrameworkCore;
using TaskManagementSystem.Api.Dtos.Tasks;
using TaskManagementSystem.Api.Models;
using TaskManagementSystem.Api.Services;
using TaskManagementSystem.Api.Tests.Infrastructure;

namespace TaskManagementSystem.Api.Tests.Services;

public class TaskServiceTests : IClassFixture<TestDatabaseFixture>
{
    private readonly TestDatabaseFixture _db;

    public TaskServiceTests(TestDatabaseFixture db)
    {
        _db = db;
    }

    private static TaskRequest NewRequest(
        long? assigneeId = null, string? status = null, string? priority = null) => new(
        assigneeId,
        TestData.Unique("ut-task"),
        "説明",
        status,
        priority,
        new DateOnly(2026, 10, 31));

    [Fact(DisplayName = "UT-501 存在しないプロジェクトのタスク一覧取得")]
    public async Task GetByProjectAsync_ReturnsNull_WhenProjectNotExists()
    {
        await using var arrange = _db.CreateContext();
        var user = await TestData.CreateUserAsync(arrange);

        await using var context = _db.CreateContext();
        var result = await new TaskService(context).GetByProjectAsync(TestData.NonExistentId, user.Id);

        Assert.Null(result);
    }

    [Fact(DisplayName = "UT-502 タスク作成成功")]
    public async Task CreateAsync_ReturnsSuccess_WithTask()
    {
        await using var arrange = _db.CreateContext();
        var (project, owner) = await TestData.CreateProjectWithOwnerAsync(arrange);
        var assignee = await TestData.CreateUserAsync(arrange);
        await TestData.AddMemberAsync(arrange, project.Id, assignee.Id);
        var request = NewRequest(assignee.Id, TaskItemStatus.InProgress, TaskItemPriority.High);

        await using var context = _db.CreateContext();
        var outcome = await new TaskService(context).CreateAsync(project.Id, request, owner.Id);

        Assert.Equal(CreateTaskResult.Success, outcome.Result);
        Assert.NotNull(outcome.Data);
        Assert.True(outcome.Data.Id > 0);
        Assert.Equal(project.Id, outcome.Data.ProjectId);
        Assert.Equal(assignee.Id, outcome.Data.AssigneeId);
        Assert.Equal(request.Title, outcome.Data.Title);
        Assert.Equal(request.Description, outcome.Data.Description);
        Assert.Equal(TaskItemStatus.InProgress, outcome.Data.Status);
        Assert.Equal(TaskItemPriority.High, outcome.Data.Priority);
        Assert.Equal(request.DueDate, outcome.Data.DueDate);
    }

    [Fact(DisplayName = "UT-503 存在しないプロジェクトへのタスク作成")]
    public async Task CreateAsync_ReturnsProjectNotFound_WhenProjectNotExists()
    {
        await using var arrange = _db.CreateContext();
        var user = await TestData.CreateUserAsync(arrange);

        await using var context = _db.CreateContext();
        var outcome = await new TaskService(context).CreateAsync(TestData.NonExistentId, NewRequest(), user.Id);

        Assert.Equal(CreateTaskResult.ProjectNotFound, outcome.Result);
    }

    [Fact(DisplayName = "UT-504 存在しない担当者を指定したタスク作成")]
    public async Task CreateAsync_ReturnsAssigneeNotFound_WhenAssigneeNotExists()
    {
        await using var arrange = _db.CreateContext();
        var (project, owner) = await TestData.CreateProjectWithOwnerAsync(arrange);

        await using var context = _db.CreateContext();
        var outcome = await new TaskService(context).CreateAsync(project.Id, NewRequest(TestData.NonExistentId), owner.Id);

        Assert.Equal(CreateTaskResult.AssigneeNotFound, outcome.Result);
    }

    [Fact(DisplayName = "UT-505 assigneeId未指定でのタスク作成")]
    public async Task CreateAsync_Succeeds_WhenAssigneeIsNull()
    {
        await using var arrange = _db.CreateContext();
        var (project, owner) = await TestData.CreateProjectWithOwnerAsync(arrange);

        await using var context = _db.CreateContext();
        var outcome = await new TaskService(context).CreateAsync(project.Id, NewRequest(assigneeId: null), owner.Id);

        Assert.Equal(CreateTaskResult.Success, outcome.Result);
        Assert.Null(outcome.Data!.AssigneeId);
    }

    [Fact(DisplayName = "UT-506 status/priority省略時の初期値")]
    public async Task CreateAsync_DefaultsStatusAndPriority()
    {
        await using var arrange = _db.CreateContext();
        var (project, owner) = await TestData.CreateProjectWithOwnerAsync(arrange);

        await using var context = _db.CreateContext();
        var outcome = await new TaskService(context).CreateAsync(project.Id, NewRequest(status: null, priority: null), owner.Id);

        Assert.Equal(TaskItemStatus.Todo, outcome.Data!.Status);
        Assert.Equal(TaskItemPriority.Medium, outcome.Data.Priority);
    }

    [Fact(DisplayName = "UT-507 タスク更新成功")]
    public async Task UpdateAsync_ReturnsSuccess_WithUpdatedTask()
    {
        await using var arrange = _db.CreateContext();
        var (project, owner) = await TestData.CreateProjectWithOwnerAsync(arrange);
        var task = await TestData.CreateTaskAsync(arrange, project.Id);
        var assignee = await TestData.CreateUserAsync(arrange);
        await TestData.AddMemberAsync(arrange, project.Id, assignee.Id);
        var request = NewRequest(assignee.Id, TaskItemStatus.Done, TaskItemPriority.Low);

        await using var context = _db.CreateContext();
        var outcome = await new TaskService(context).UpdateAsync(task.Id, request, owner.Id);

        Assert.Equal(UpdateTaskResult.Success, outcome.Result);
        Assert.Equal(
            new TaskDto(task.Id, project.Id, assignee.Id, request.Title, request.Description,
                TaskItemStatus.Done, TaskItemPriority.Low, request.DueDate),
            outcome.Data);

        await using var assert = _db.CreateContext();
        var saved = await assert.Tasks.SingleAsync(t => t.Id == task.Id);
        Assert.Equal(request.Title, saved.Title);
        Assert.Equal(TaskItemStatus.Done, saved.Status);
    }

    [Fact(DisplayName = "UT-508 存在しないタスクの更新")]
    public async Task UpdateAsync_ReturnsTaskNotFound_WhenTaskNotExists()
    {
        await using var arrange = _db.CreateContext();
        var user = await TestData.CreateUserAsync(arrange);

        await using var context = _db.CreateContext();
        var outcome = await new TaskService(context).UpdateAsync(TestData.NonExistentId, NewRequest(), user.Id);

        Assert.Equal(UpdateTaskResult.TaskNotFound, outcome.Result);
    }

    [Fact(DisplayName = "UT-509 存在しない担当者への更新")]
    public async Task UpdateAsync_ReturnsAssigneeNotFound_WhenAssigneeNotExists()
    {
        await using var arrange = _db.CreateContext();
        var (project, owner) = await TestData.CreateProjectWithOwnerAsync(arrange);
        var task = await TestData.CreateTaskAsync(arrange, project.Id);

        await using var context = _db.CreateContext();
        var outcome = await new TaskService(context).UpdateAsync(task.Id, NewRequest(TestData.NonExistentId), owner.Id);

        Assert.Equal(UpdateTaskResult.AssigneeNotFound, outcome.Result);
    }

    [Fact(DisplayName = "UT-510 タスク削除成功")]
    public async Task DeleteAsync_ReturnsSuccess_AndDeletesRow()
    {
        await using var arrange = _db.CreateContext();
        var (project, owner) = await TestData.CreateProjectWithOwnerAsync(arrange);
        var task = await TestData.CreateTaskAsync(arrange, project.Id);

        await using var context = _db.CreateContext();
        var result = await new TaskService(context).DeleteAsync(task.Id, owner.Id);

        Assert.Equal(DeleteTaskResult.Success, result);
        await using var assert = _db.CreateContext();
        Assert.False(await assert.Tasks.AnyAsync(t => t.Id == task.Id));
    }

    [Fact(DisplayName = "UT-511 存在しないタスクの削除")]
    public async Task DeleteAsync_ReturnsTaskNotFound_WhenTaskNotExists()
    {
        await using var arrange = _db.CreateContext();
        var user = await TestData.CreateUserAsync(arrange);

        await using var context = _db.CreateContext();
        var result = await new TaskService(context).DeleteAsync(TestData.NonExistentId, user.Id);

        Assert.Equal(DeleteTaskResult.TaskNotFound, result);
    }

    [Fact(DisplayName = "UT-512 所属していないプロジェクトのタスク操作")]
    public async Task TaskOperations_ReturnNotFound_WhenUserIsNotMember()
    {
        await using var arrange = _db.CreateContext();
        var (project, _) = await TestData.CreateProjectWithOwnerAsync(arrange);
        var task = await TestData.CreateTaskAsync(arrange, project.Id);
        var outsider = await TestData.CreateUserAsync(arrange);

        await using var context = _db.CreateContext();
        var service = new TaskService(context);
        var list = await service.GetByProjectAsync(project.Id, outsider.Id);
        var detail = await service.GetByIdAsync(task.Id, outsider.Id);
        var createOutcome = await service.CreateAsync(project.Id, NewRequest(), outsider.Id);
        var updateOutcome = await service.UpdateAsync(task.Id, NewRequest(), outsider.Id);
        var deleteResult = await service.DeleteAsync(task.Id, outsider.Id);

        Assert.Null(list);
        Assert.Null(detail);
        Assert.Equal(CreateTaskResult.ProjectNotFound, createOutcome.Result);
        Assert.Equal(UpdateTaskResult.TaskNotFound, updateOutcome.Result);
        Assert.Equal(DeleteTaskResult.TaskNotFound, deleteResult);
        await using var assert = _db.CreateContext();
        var saved = await assert.Tasks.SingleAsync(t => t.Id == task.Id);
        Assert.Equal(task.Title, saved.Title);
        Assert.Equal(1, await assert.Tasks.CountAsync(t => t.ProjectId == project.Id));
    }

    [Fact(DisplayName = "UT-513 MEMBERはタスクの参照・作成・更新ができる")]
    public async Task TaskOperations_Succeed_WhenUserIsMember()
    {
        await using var arrange = _db.CreateContext();
        var (project, _) = await TestData.CreateProjectWithOwnerAsync(arrange);
        var task = await TestData.CreateTaskAsync(arrange, project.Id);
        var member = await TestData.CreateUserAsync(arrange);
        await TestData.AddMemberAsync(arrange, project.Id, member.Id, ProjectMemberRole.Member);

        await using var context = _db.CreateContext();
        var service = new TaskService(context);
        var list = await service.GetByProjectAsync(project.Id, member.Id);
        var detail = await service.GetByIdAsync(task.Id, member.Id);
        var createOutcome = await service.CreateAsync(project.Id, NewRequest(member.Id), member.Id);
        var updateOutcome = await service.UpdateAsync(task.Id, NewRequest(member.Id), member.Id);

        Assert.NotNull(list);
        Assert.NotNull(detail);
        Assert.Equal(CreateTaskResult.Success, createOutcome.Result);
        Assert.Equal(UpdateTaskResult.Success, updateOutcome.Result);
    }

    [Fact(DisplayName = "UT-514 MEMBERによるタスク削除")]
    public async Task DeleteAsync_ReturnsForbidden_WhenUserIsMember()
    {
        await using var arrange = _db.CreateContext();
        var (project, _) = await TestData.CreateProjectWithOwnerAsync(arrange);
        var task = await TestData.CreateTaskAsync(arrange, project.Id);
        var member = await TestData.CreateUserAsync(arrange);
        await TestData.AddMemberAsync(arrange, project.Id, member.Id, ProjectMemberRole.Member);

        await using var context = _db.CreateContext();
        var result = await new TaskService(context).DeleteAsync(task.Id, member.Id);

        Assert.Equal(DeleteTaskResult.Forbidden, result);
        await using var assert = _db.CreateContext();
        Assert.True(await assert.Tasks.AnyAsync(t => t.Id == task.Id));
    }

    [Fact(DisplayName = "UT-515 プロジェクトに所属していないユーザーを担当者に指定")]
    public async Task CreateAndUpdateAsync_ReturnAssigneeNotMember_WhenAssigneeIsNotMember()
    {
        await using var arrange = _db.CreateContext();
        var (project, owner) = await TestData.CreateProjectWithOwnerAsync(arrange);
        var task = await TestData.CreateTaskAsync(arrange, project.Id);
        var outsider = await TestData.CreateUserAsync(arrange);

        await using var context = _db.CreateContext();
        var service = new TaskService(context);
        var createOutcome = await service.CreateAsync(project.Id, NewRequest(outsider.Id), owner.Id);
        var updateOutcome = await service.UpdateAsync(task.Id, NewRequest(outsider.Id), owner.Id);

        Assert.Equal(CreateTaskResult.AssigneeNotMember, createOutcome.Result);
        Assert.Equal(UpdateTaskResult.AssigneeNotMember, updateOutcome.Result);
        await using var assert = _db.CreateContext();
        Assert.Null((await assert.Tasks.SingleAsync(t => t.Id == task.Id)).AssigneeId);
    }
}
