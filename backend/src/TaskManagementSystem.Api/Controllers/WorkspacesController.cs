using Microsoft.AspNetCore.Mvc;
using TaskManagementSystem.Api.Dtos.Common;
using TaskManagementSystem.Api.Dtos.Workspaces;
using TaskManagementSystem.Api.Extensions;
using TaskManagementSystem.Api.Services;

namespace TaskManagementSystem.Api.Controllers;

[ApiController]
[Route("api/workspaces")]
public class WorkspacesController : ControllerBase
{
    private readonly IWorkspaceService _workspaceService;

    public WorkspacesController(IWorkspaceService workspaceService)
    {
        _workspaceService = workspaceService;
    }

    /// <summary>全ワークスペースの横断一覧(System Admin のみ)。</summary>
    [HttpGet]
    public async Task<ActionResult<List<WorkspaceDto>>> GetAll()
    {
        var workspaces = await _workspaceService.GetAllAsync(this.GetCurrentUserId());
        if (workspaces is null)
        {
            // 一般ユーザーには全件一覧を許可しない(所属分は GET /api/me/workspaces)
            return this.ForbiddenError();
        }

        return Ok(workspaces);
    }

    [HttpGet("{workspaceId}")]
    public async Task<ActionResult<WorkspaceDto>> GetById(long workspaceId)
    {
        var workspace = await _workspaceService.GetByIdAsync(workspaceId, this.GetCurrentUserId());
        if (workspace is null)
        {
            return NotFound(new ErrorResponse("指定されたワークスペースが存在しません。"));
        }

        return Ok(workspace);
    }

    [HttpPost]
    public async Task<ActionResult<WorkspaceDto>> Create(WorkspaceRequest request)
    {
        var outcome = await _workspaceService.CreateAsync(request, this.GetCurrentUserId());

        return outcome.Result switch
        {
            CreateWorkspaceResult.Forbidden => this.ForbiddenError(),

            _ => CreatedAtAction(nameof(GetById), new { workspaceId = outcome.Data!.Id }, outcome.Data),
        };
    }

    [HttpPatch("{workspaceId}")]
    public async Task<ActionResult<WorkspaceDto>> Update(long workspaceId, WorkspaceRequest request)
    {
        var outcome = await _workspaceService.UpdateAsync(workspaceId, request, this.GetCurrentUserId());

        return outcome.Result switch
        {
            UpdateWorkspaceResult.NotFound =>
                NotFound(new ErrorResponse("指定されたワークスペースが存在しません。")),

            UpdateWorkspaceResult.Forbidden => this.ForbiddenError(),

            _ => Ok(outcome.Data),
        };
    }

    [HttpPost("{workspaceId}/archive")]
    public async Task<IActionResult> Archive(long workspaceId)
    {
        var result = await _workspaceService.ArchiveAsync(workspaceId, this.GetCurrentUserId());

        return result switch
        {
            ArchiveWorkspaceResult.NotFound =>
                NotFound(new ErrorResponse("指定されたワークスペースが存在しません。")),

            ArchiveWorkspaceResult.Forbidden => this.ForbiddenError(),

            _ => NoContent(),
        };
    }
}
