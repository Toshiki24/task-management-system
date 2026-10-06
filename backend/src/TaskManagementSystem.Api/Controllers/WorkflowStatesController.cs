using Microsoft.AspNetCore.Mvc;
using TaskManagementSystem.Api.Dtos.Common;
using TaskManagementSystem.Api.Dtos.Workflow;
using TaskManagementSystem.Api.Extensions;
using TaskManagementSystem.Api.Services;

namespace TaskManagementSystem.Api.Controllers;

[ApiController]
[Route("api/workspaces/{workspaceId}/workflow-states")]
public class WorkflowStatesController : ControllerBase
{
    private readonly IWorkflowStateService _service;

    public WorkflowStatesController(IWorkflowStateService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<ActionResult<List<WorkflowStateDto>>> GetAll(long workspaceId)
    {
        var states = await _service.GetByWorkspaceAsync(workspaceId, this.GetCurrentUserId());
        if (states is null)
        {
            return NotFound(new ErrorResponse("指定されたワークスペースが存在しません。"));
        }

        return Ok(states);
    }

    [HttpPost]
    public async Task<ActionResult<WorkflowStateDto>> Create(
        long workspaceId, WorkflowStateCreateRequest request)
    {
        var outcome = await _service.CreateAsync(workspaceId, request, this.GetCurrentUserId());

        return outcome.Result switch
        {
            CreateWorkflowStateResult.WorkspaceNotFound =>
                NotFound(new ErrorResponse("指定されたワークスペースが存在しません。")),

            CreateWorkflowStateResult.Forbidden => this.ForbiddenError(),

            CreateWorkflowStateResult.DuplicateKey =>
                Conflict(new ErrorResponse("同じ状態キーが既に存在します。")),

            _ => CreatedAtAction(nameof(GetAll), new { workspaceId }, outcome.Data),
        };
    }

    [HttpPatch("{stateId}")]
    public async Task<ActionResult<WorkflowStateDto>> Update(
        long workspaceId, long stateId, WorkflowStateUpdateRequest request)
    {
        var outcome = await _service.UpdateAsync(workspaceId, stateId, request, this.GetCurrentUserId());

        return outcome.Result switch
        {
            UpdateWorkflowStateResult.NotFound =>
                NotFound(new ErrorResponse("指定された状態が存在しません。")),

            UpdateWorkflowStateResult.Forbidden => this.ForbiddenError(),

            _ => Ok(outcome.Data),
        };
    }

    [HttpDelete("{stateId}")]
    public async Task<IActionResult> Delete(
        long workspaceId, long stateId, [FromQuery] string? moveTo)
    {
        var result = await _service.DeleteAsync(workspaceId, stateId, moveTo, this.GetCurrentUserId());

        return result switch
        {
            DeleteWorkflowStateResult.NotFound =>
                NotFound(new ErrorResponse("指定された状態が存在しません。")),

            DeleteWorkflowStateResult.Forbidden => this.ForbiddenError(),

            DeleteWorkflowStateResult.DefaultState =>
                Conflict(new ErrorResponse("既定の状態は削除できません。先に別の状態を既定にしてください。")),

            DeleteWorkflowStateResult.InUse =>
                Conflict(new ErrorResponse("この状態を使っているタスクがあります。付け替え先の状態(moveTo)を指定してください。")),

            _ => NoContent(),
        };
    }
}
