using Microsoft.AspNetCore.Mvc;
using TaskManagementSystem.Api.Dtos.Common;
using TaskManagementSystem.Api.Dtos.Labels;
using TaskManagementSystem.Api.Extensions;
using TaskManagementSystem.Api.Services;

namespace TaskManagementSystem.Api.Controllers;

[ApiController]
[Route("api/workspaces/{workspaceId}/labels")]
public class LabelsController : ControllerBase
{
    private readonly ILabelService _service;

    public LabelsController(ILabelService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<ActionResult<List<LabelDto>>> GetAll(long workspaceId)
    {
        var labels = await _service.GetByWorkspaceAsync(workspaceId, this.GetCurrentUserId());
        if (labels is null)
        {
            return NotFound(new ErrorResponse("指定されたワークスペースが存在しません。"));
        }

        return Ok(labels);
    }

    [HttpPost]
    public async Task<ActionResult<LabelDto>> Create(long workspaceId, LabelRequest request)
    {
        var outcome = await _service.CreateAsync(workspaceId, request, this.GetCurrentUserId());

        return outcome.Result switch
        {
            CreateLabelResult.WorkspaceNotFound =>
                NotFound(new ErrorResponse("指定されたワークスペースが存在しません。")),

            CreateLabelResult.Forbidden => this.ForbiddenError(),

            CreateLabelResult.DuplicateName =>
                Conflict(new ErrorResponse("同じ名前のラベルが既に存在します。")),

            _ => CreatedAtAction(nameof(GetAll), new { workspaceId }, outcome.Data),
        };
    }

    [HttpPatch("{labelId}")]
    public async Task<ActionResult<LabelDto>> Update(long workspaceId, long labelId, LabelRequest request)
    {
        var outcome = await _service.UpdateAsync(workspaceId, labelId, request, this.GetCurrentUserId());

        return outcome.Result switch
        {
            UpdateLabelResult.NotFound =>
                NotFound(new ErrorResponse("指定されたラベルが存在しません。")),

            UpdateLabelResult.Forbidden => this.ForbiddenError(),

            UpdateLabelResult.DuplicateName =>
                Conflict(new ErrorResponse("同じ名前のラベルが既に存在します。")),

            _ => Ok(outcome.Data),
        };
    }

    [HttpDelete("{labelId}")]
    public async Task<IActionResult> Delete(long workspaceId, long labelId)
    {
        var result = await _service.DeleteAsync(workspaceId, labelId, this.GetCurrentUserId());

        return result switch
        {
            DeleteLabelResult.NotFound =>
                NotFound(new ErrorResponse("指定されたラベルが存在しません。")),

            DeleteLabelResult.Forbidden => this.ForbiddenError(),

            _ => NoContent(),
        };
    }
}
