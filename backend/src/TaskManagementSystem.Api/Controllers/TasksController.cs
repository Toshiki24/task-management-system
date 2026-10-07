using Microsoft.AspNetCore.Mvc;
using TaskManagementSystem.Api.Dtos.Common;
using TaskManagementSystem.Api.Dtos.Tasks;
using TaskManagementSystem.Api.Extensions;
using TaskManagementSystem.Api.Services;

namespace TaskManagementSystem.Api.Controllers;

[ApiController]
[Route("api/tasks")]
public class TasksController : ControllerBase
{
    private readonly ITaskService _taskService;

    public TasksController(ITaskService taskService)
    {
        _taskService = taskService;
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<TaskDto>> GetById(long id)
    {
        var task = await _taskService.GetByIdAsync(id, this.GetCurrentUserId());
        if (task is null)
        {
            return NotFound(new ErrorResponse("指定されたタスクが存在しません。"));
        }

        return Ok(task);
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<TaskDto>> Update(long id, TaskRequest request)
    {
        var outcome = await _taskService.UpdateAsync(id, request, this.GetCurrentUserId());

        return outcome.Result switch
        {
            UpdateTaskResult.TaskNotFound =>
                NotFound(new ErrorResponse("指定されたタスクが存在しません。")),

            UpdateTaskResult.Forbidden => this.ForbiddenError(),

            UpdateTaskResult.AssigneeNotFound =>
                BadRequest(new ValidationErrorResponse(
                    "入力内容に誤りがあります。",
                    new[] { new ValidationErrorItem("assigneeId", "指定されたユーザーが存在しません。") })),

            UpdateTaskResult.AssigneeNotMember =>
                BadRequest(new ValidationErrorResponse(
                    "入力内容に誤りがあります。",
                    new[] { new ValidationErrorItem("assigneeId", "指定されたユーザーはプロジェクトのメンバーではありません。") })),

            UpdateTaskResult.InvalidStatus =>
                BadRequest(new ValidationErrorResponse(
                    "入力内容に誤りがあります。",
                    new[] { new ValidationErrorItem("status", "タスク状態の値が不正です。") })),

            UpdateTaskResult.InvalidLabel =>
                BadRequest(new ValidationErrorResponse(
                    "入力内容に誤りがあります。",
                    new[] { new ValidationErrorItem("labelIds", "指定されたラベルが存在しません。") })),

            _ => Ok(outcome.Data),
        };
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(long id)
    {
        var result = await _taskService.DeleteAsync(id, this.GetCurrentUserId());

        return result switch
        {
            DeleteTaskResult.TaskNotFound =>
                NotFound(new ErrorResponse("指定されたタスクが存在しません。")),

            DeleteTaskResult.Forbidden => this.ForbiddenError(),

            _ => NoContent(),
        };
    }
}
