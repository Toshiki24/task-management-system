using Microsoft.AspNetCore.Mvc;
using TaskManagementSystem.Api.Dtos.Common;
using TaskManagementSystem.Api.Dtos.Cycles;
using TaskManagementSystem.Api.Dtos.Milestones;
using TaskManagementSystem.Api.Dtos.Tasks;
using TaskManagementSystem.Api.Extensions;
using TaskManagementSystem.Api.Services;

namespace TaskManagementSystem.Api.Controllers;

[ApiController]
[Route("api/projects/{projectId}/tasks")]
public class ProjectTasksController : ControllerBase
{
    private readonly ITaskService _taskService;
    private readonly ICycleService _cycleService;
    private readonly IMilestoneService _milestoneService;

    public ProjectTasksController(ITaskService taskService, ICycleService cycleService, IMilestoneService milestoneService)
    {
        _taskService = taskService;
        _cycleService = cycleService;
        _milestoneService = milestoneService;
    }

    [HttpPatch("{taskId}/cycle")]
    public async Task<IActionResult> AssignCycle(long projectId, long taskId, AssignCycleRequest request)
    {
        var result = await _cycleService.AssignTaskAsync(projectId, taskId, request.CycleId, this.GetCurrentUserId());

        return result switch
        {
            CycleResult.ProjectNotFound =>
                NotFound(new ErrorResponse("指定されたプロジェクトが存在しません。")),
            CycleResult.Forbidden => this.ForbiddenError(),
            CycleResult.InvalidTask =>
                NotFound(new ErrorResponse("指定されたタスクが存在しません。")),
            CycleResult.InvalidCycle =>
                BadRequest(new ValidationErrorResponse(
                    "入力内容に誤りがあります。",
                    new[] { new ValidationErrorItem("cycleId", "指定されたサイクルが不正です。") })),
            _ => NoContent(),
        };
    }

    [HttpPatch("{taskId}/milestone")]
    public async Task<IActionResult> AssignMilestone(long projectId, long taskId, AssignMilestoneRequest request)
    {
        var result = await _milestoneService.AssignTaskAsync(projectId, taskId, request.MilestoneId, this.GetCurrentUserId());

        return result switch
        {
            MilestoneResult.ProjectNotFound =>
                NotFound(new ErrorResponse("指定されたプロジェクトが存在しません。")),
            MilestoneResult.Forbidden => this.ForbiddenError(),
            MilestoneResult.InvalidTask =>
                NotFound(new ErrorResponse("指定されたタスクが存在しません。")),
            MilestoneResult.InvalidMilestone =>
                BadRequest(new ValidationErrorResponse(
                    "入力内容に誤りがあります。",
                    new[] { new ValidationErrorItem("milestoneId", "指定されたマイルストーンが不正です。") })),
            _ => NoContent(),
        };
    }

    [HttpGet]
    public async Task<ActionResult<List<TaskDto>>> GetAll(long projectId, [FromQuery] TaskListQuery query)
    {
        var tasks = await _taskService.GetByProjectAsync(projectId, this.GetCurrentUserId(), query);
        if (tasks is null)
        {
            return NotFound(new ErrorResponse("指定されたプロジェクトが存在しません。"));
        }

        return Ok(tasks);
    }

    [HttpPost]
    public async Task<ActionResult<TaskDto>> Create(long projectId, TaskRequest request)
    {
        var outcome = await _taskService.CreateAsync(projectId, request, this.GetCurrentUserId());

        return outcome.Result switch
        {
            CreateTaskResult.ProjectNotFound =>
                NotFound(new ErrorResponse("指定されたプロジェクトが存在しません。")),

            CreateTaskResult.Forbidden => this.ForbiddenError(),

            CreateTaskResult.AssigneeNotFound =>
                BadRequest(new ValidationErrorResponse(
                    "入力内容に誤りがあります。",
                    new[] { new ValidationErrorItem("assigneeId", "指定されたユーザーが存在しません。") })),

            CreateTaskResult.AssigneeNotMember =>
                BadRequest(new ValidationErrorResponse(
                    "入力内容に誤りがあります。",
                    new[] { new ValidationErrorItem("assigneeId", "指定されたユーザーはプロジェクトのメンバーではありません。") })),

            CreateTaskResult.InvalidStatus =>
                BadRequest(new ValidationErrorResponse(
                    "入力内容に誤りがあります。",
                    new[] { new ValidationErrorItem("status", "タスク状態の値が不正です。") })),

            CreateTaskResult.InvalidLabel =>
                BadRequest(new ValidationErrorResponse(
                    "入力内容に誤りがあります。",
                    new[] { new ValidationErrorItem("labelIds", "指定されたラベルが存在しません。") })),

            CreateTaskResult.InvalidParent =>
                BadRequest(new ValidationErrorResponse(
                    "入力内容に誤りがあります。",
                    new[] { new ValidationErrorItem("parentTaskId", "親タスクが不正です(同一プロジェクトの親タスクを指定してください)。") })),

            _ => Created($"/api/tasks/{outcome.Data!.Id}", outcome.Data),
        };
    }

    [HttpPatch("bulk")]
    public async Task<ActionResult<BulkUpdateTasksResponse>> BulkUpdate(long projectId, BulkUpdateTasksRequest request)
    {
        var outcome = await _taskService.BulkUpdateAsync(projectId, request, this.GetCurrentUserId());

        return outcome.Result switch
        {
            BulkUpdateResult.ProjectNotFound =>
                NotFound(new ErrorResponse("指定されたプロジェクトが存在しません。")),

            BulkUpdateResult.Forbidden => this.ForbiddenError(),

            BulkUpdateResult.InvalidTask =>
                BadRequest(new ValidationErrorResponse(
                    "入力内容に誤りがあります。",
                    new[] { new ValidationErrorItem("taskIds", "対象タスクが不正です(同一プロジェクトのタスクを指定してください)。") })),

            BulkUpdateResult.InvalidStatus =>
                BadRequest(new ValidationErrorResponse(
                    "入力内容に誤りがあります。",
                    new[] { new ValidationErrorItem("status", "タスク状態の値が不正です。") })),

            BulkUpdateResult.InvalidLabel =>
                BadRequest(new ValidationErrorResponse(
                    "入力内容に誤りがあります。",
                    new[] { new ValidationErrorItem("labelIds", "指定されたラベルが存在しません。") })),

            BulkUpdateResult.AssigneeNotFound =>
                BadRequest(new ValidationErrorResponse(
                    "入力内容に誤りがあります。",
                    new[] { new ValidationErrorItem("assigneeId", "指定されたユーザーが存在しません。") })),

            BulkUpdateResult.AssigneeNotMember =>
                BadRequest(new ValidationErrorResponse(
                    "入力内容に誤りがあります。",
                    new[] { new ValidationErrorItem("assigneeId", "指定されたユーザーはプロジェクトのメンバーではありません。") })),

            _ => Ok(new BulkUpdateTasksResponse(outcome.Updated)),
        };
    }

    [HttpPatch("{taskId}/move")]
    public async Task<ActionResult<TaskDto>> Move(long projectId, long taskId, MoveTaskRequest request)
    {
        var outcome = await _taskService.MoveAsync(taskId, request, this.GetCurrentUserId());

        return outcome.Result switch
        {
            MoveTaskResult.TaskNotFound =>
                NotFound(new ErrorResponse("指定されたタスクが存在しません。")),

            MoveTaskResult.Forbidden => this.ForbiddenError(),

            MoveTaskResult.InvalidStatus =>
                BadRequest(new ValidationErrorResponse(
                    "入力内容に誤りがあります。",
                    new[] { new ValidationErrorItem("toStatus", "タスク状態の値が不正です。") })),

            _ => Ok(outcome.Data),
        };
    }
}
