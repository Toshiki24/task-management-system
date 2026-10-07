using Microsoft.AspNetCore.Mvc;
using TaskManagementSystem.Api.Dtos.Common;
using TaskManagementSystem.Api.Dtos.Tasks;
using TaskManagementSystem.Api.Extensions;
using TaskManagementSystem.Api.Services;

namespace TaskManagementSystem.Api.Controllers;

[ApiController]
[Route("api/tasks/{taskId}/dependencies")]
public class TaskDependenciesController : ControllerBase
{
    private readonly IDependencyService _service;

    public TaskDependenciesController(IDependencyService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<ActionResult<TaskDependenciesDto>> Get(long taskId)
    {
        var dependencies = await _service.GetByTaskAsync(taskId, this.GetCurrentUserId());
        if (dependencies is null)
        {
            return NotFound(new ErrorResponse("指定されたタスクが存在しません。"));
        }

        return Ok(dependencies);
    }

    [HttpPost]
    public async Task<ActionResult<DependencyLinkDto>> Add(long taskId, AddDependencyRequest request)
    {
        var outcome = await _service.AddAsync(taskId, request, this.GetCurrentUserId());

        return outcome.Result switch
        {
            AddDependencyResult.TaskNotFound =>
                NotFound(new ErrorResponse("指定されたタスクが存在しません。")),

            AddDependencyResult.Forbidden => this.ForbiddenError(),

            AddDependencyResult.InvalidTarget =>
                BadRequest(new ValidationErrorResponse(
                    "入力内容に誤りがあります。",
                    new[] { new ValidationErrorItem("taskId", "対象タスクが不正です(同一プロジェクトの別タスクを指定してください)。") })),

            AddDependencyResult.InvalidRelation =>
                BadRequest(new ValidationErrorResponse(
                    "入力内容に誤りがあります。",
                    new[] { new ValidationErrorItem("relation", "関係の種類が不正です。") })),

            AddDependencyResult.Duplicate =>
                Conflict(new ErrorResponse("同じ依存関係が既に登録されています。")),

            AddDependencyResult.Cycle =>
                Conflict(new ErrorResponse("依存関係が循環するため追加できません。")),

            _ => Created($"/api/tasks/{taskId}/dependencies/{outcome.Data!.DependencyId}", outcome.Data),
        };
    }

    [HttpDelete("{dependencyId}")]
    public async Task<IActionResult> Delete(long taskId, long dependencyId)
    {
        var result = await _service.DeleteAsync(taskId, dependencyId, this.GetCurrentUserId());

        return result switch
        {
            DeleteDependencyResult.NotFound =>
                NotFound(new ErrorResponse("指定された依存関係が存在しません。")),

            DeleteDependencyResult.Forbidden => this.ForbiddenError(),

            _ => NoContent(),
        };
    }
}
