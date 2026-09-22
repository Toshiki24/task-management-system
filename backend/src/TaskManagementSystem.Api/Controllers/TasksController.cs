using Microsoft.AspNetCore.Mvc;
using TaskManagementSystem.Api.Dtos.Common;
using TaskManagementSystem.Api.Dtos.Tasks;
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
        var task = await _taskService.GetByIdAsync(id);
        if (task is null)
        {
            return NotFound(new ErrorResponse("指定されたタスクが存在しません。"));
        }

        return Ok(task);
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<TaskDto>> Update(long id, TaskRequest request)
    {
        var outcome = await _taskService.UpdateAsync(id, request);

        return outcome.Result switch
        {
            UpdateTaskResult.TaskNotFound =>
                NotFound(new ErrorResponse("指定されたタスクが存在しません。")),

            UpdateTaskResult.AssigneeNotFound =>
                BadRequest(new ValidationErrorResponse(
                    "入力内容に誤りがあります。",
                    new[] { new ValidationErrorItem("assigneeId", "指定されたユーザーが存在しません。") })),

            _ => Ok(outcome.Data),
        };
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(long id)
    {
        var deleted = await _taskService.DeleteAsync(id);
        if (!deleted)
        {
            return NotFound(new ErrorResponse("指定されたタスクが存在しません。"));
        }

        return NoContent();
    }
}
