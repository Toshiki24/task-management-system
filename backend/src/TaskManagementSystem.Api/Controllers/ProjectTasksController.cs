using Microsoft.AspNetCore.Mvc;
using TaskManagementSystem.Api.Dtos.Common;
using TaskManagementSystem.Api.Dtos.Tasks;
using TaskManagementSystem.Api.Services;

namespace TaskManagementSystem.Api.Controllers;

[ApiController]
[Route("api/projects/{projectId}/tasks")]
public class ProjectTasksController : ControllerBase
{
    private readonly ITaskService _taskService;

    public ProjectTasksController(ITaskService taskService)
    {
        _taskService = taskService;
    }

    [HttpGet]
    public async Task<ActionResult<List<TaskDto>>> GetAll(long projectId)
    {
        var tasks = await _taskService.GetByProjectAsync(projectId);
        if (tasks is null)
        {
            return NotFound(new ErrorResponse("指定されたプロジェクトが存在しません。"));
        }

        return Ok(tasks);
    }

    [HttpPost]
    public async Task<ActionResult<TaskDto>> Create(long projectId, TaskRequest request)
    {
        var outcome = await _taskService.CreateAsync(projectId, request);

        return outcome.Result switch
        {
            CreateTaskResult.ProjectNotFound =>
                NotFound(new ErrorResponse("指定されたプロジェクトが存在しません。")),

            CreateTaskResult.AssigneeNotFound =>
                BadRequest(new ValidationErrorResponse(
                    "入力内容に誤りがあります。",
                    new[] { new ValidationErrorItem("assigneeId", "指定されたユーザーが存在しません。") })),

            _ => Created($"/api/tasks/{outcome.Data!.Id}", outcome.Data),
        };
    }
}
