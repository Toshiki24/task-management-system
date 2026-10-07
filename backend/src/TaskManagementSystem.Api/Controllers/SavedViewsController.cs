using Microsoft.AspNetCore.Mvc;
using TaskManagementSystem.Api.Dtos.Common;
using TaskManagementSystem.Api.Dtos.Views;
using TaskManagementSystem.Api.Extensions;
using TaskManagementSystem.Api.Services;

namespace TaskManagementSystem.Api.Controllers;

[ApiController]
[Route("api/workspaces/{workspaceId}/views")]
public class SavedViewsController : ControllerBase
{
    private readonly ISavedViewService _service;

    public SavedViewsController(ISavedViewService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<ActionResult<List<SavedViewDto>>> GetAll(long workspaceId)
    {
        var views = await _service.GetByWorkspaceAsync(workspaceId, this.GetCurrentUserId());
        if (views is null)
        {
            return NotFound(new ErrorResponse("指定されたワークスペースが存在しません。"));
        }

        return Ok(views);
    }

    [HttpPost]
    public async Task<ActionResult<SavedViewDto>> Create(long workspaceId, SavedViewRequest request)
    {
        var outcome = await _service.CreateAsync(workspaceId, request, this.GetCurrentUserId());

        return outcome.Result switch
        {
            CreateSavedViewResult.WorkspaceNotFound =>
                NotFound(new ErrorResponse("指定されたワークスペースが存在しません。")),

            CreateSavedViewResult.Forbidden => this.ForbiddenError(),

            _ => CreatedAtAction(nameof(GetAll), new { workspaceId }, outcome.Data),
        };
    }

    [HttpPatch("{viewId}")]
    public async Task<ActionResult<SavedViewDto>> Update(long workspaceId, long viewId, SavedViewRequest request)
    {
        var outcome = await _service.UpdateAsync(workspaceId, viewId, request, this.GetCurrentUserId());

        return outcome.Result switch
        {
            UpdateSavedViewResult.NotFound =>
                NotFound(new ErrorResponse("指定されたビューが存在しません。")),

            UpdateSavedViewResult.Forbidden => this.ForbiddenError(),

            _ => Ok(outcome.Data),
        };
    }

    [HttpDelete("{viewId}")]
    public async Task<IActionResult> Delete(long workspaceId, long viewId)
    {
        var result = await _service.DeleteAsync(workspaceId, viewId, this.GetCurrentUserId());

        return result switch
        {
            DeleteSavedViewResult.NotFound =>
                NotFound(new ErrorResponse("指定されたビューが存在しません。")),

            DeleteSavedViewResult.Forbidden => this.ForbiddenError(),

            _ => NoContent(),
        };
    }
}
