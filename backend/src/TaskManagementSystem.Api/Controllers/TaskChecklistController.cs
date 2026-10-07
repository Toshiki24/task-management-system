using Microsoft.AspNetCore.Mvc;
using TaskManagementSystem.Api.Dtos.Common;
using TaskManagementSystem.Api.Dtos.Tasks;
using TaskManagementSystem.Api.Extensions;
using TaskManagementSystem.Api.Services;

namespace TaskManagementSystem.Api.Controllers;

[ApiController]
[Route("api/tasks/{taskId}/checklist")]
public class TaskChecklistController : ControllerBase
{
    private readonly IChecklistService _service;

    public TaskChecklistController(IChecklistService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<ActionResult<List<ChecklistItemDto>>> GetAll(long taskId)
    {
        var items = await _service.GetByTaskAsync(taskId, this.GetCurrentUserId());
        if (items is null)
        {
            return NotFound(new ErrorResponse("指定されたタスクが存在しません。"));
        }

        return Ok(items);
    }

    [HttpPost]
    public async Task<ActionResult<ChecklistItemDto>> Create(long taskId, ChecklistItemRequest request)
    {
        var outcome = await _service.CreateAsync(taskId, request, this.GetCurrentUserId());

        return outcome.Result switch
        {
            CreateChecklistItemResult.TaskNotFound =>
                NotFound(new ErrorResponse("指定されたタスクが存在しません。")),

            CreateChecklistItemResult.Forbidden => this.ForbiddenError(),

            _ => Created($"/api/tasks/{taskId}/checklist/{outcome.Data!.Id}", outcome.Data),
        };
    }

    [HttpPatch("{itemId}")]
    public async Task<ActionResult<ChecklistItemDto>> Update(long taskId, long itemId, ChecklistItemRequest request)
    {
        var outcome = await _service.UpdateAsync(taskId, itemId, request, this.GetCurrentUserId());

        return outcome.Result switch
        {
            UpdateChecklistItemResult.NotFound =>
                NotFound(new ErrorResponse("指定されたチェックリスト項目が存在しません。")),

            UpdateChecklistItemResult.Forbidden => this.ForbiddenError(),

            _ => Ok(outcome.Data),
        };
    }

    [HttpDelete("{itemId}")]
    public async Task<IActionResult> Delete(long taskId, long itemId)
    {
        var result = await _service.DeleteAsync(taskId, itemId, this.GetCurrentUserId());

        return result switch
        {
            DeleteChecklistItemResult.NotFound =>
                NotFound(new ErrorResponse("指定されたチェックリスト項目が存在しません。")),

            DeleteChecklistItemResult.Forbidden => this.ForbiddenError(),

            _ => NoContent(),
        };
    }
}
