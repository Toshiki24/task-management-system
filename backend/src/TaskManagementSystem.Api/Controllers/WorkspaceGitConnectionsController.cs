using Microsoft.AspNetCore.Mvc;
using TaskManagementSystem.Api.Dtos.Common;
using TaskManagementSystem.Api.Dtos.Git;
using TaskManagementSystem.Api.Extensions;
using TaskManagementSystem.Api.Services;

namespace TaskManagementSystem.Api.Controllers;

[ApiController]
[Route("api/workspaces/{workspaceId}/git-connections")]
public class WorkspaceGitConnectionsController : ControllerBase
{
    private readonly IGitConnectionService _service;

    public WorkspaceGitConnectionsController(IGitConnectionService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<ActionResult<List<GitConnectionDto>>> GetAll(long workspaceId)
    {
        var connections = await _service.GetByWorkspaceAsync(workspaceId, this.GetCurrentUserId());
        if (connections is null)
        {
            return NotFound(new ErrorResponse("指定されたワークスペースが存在しません。"));
        }

        return Ok(connections);
    }

    [HttpPost]
    public async Task<ActionResult<GitConnectionDto>> Create(long workspaceId, CreateGitConnectionRequest request)
    {
        var outcome = await _service.CreateAsync(workspaceId, request, this.GetCurrentUserId());

        return outcome.Result switch
        {
            GitConnectionResult.WorkspaceNotFound =>
                NotFound(new ErrorResponse("指定されたワークスペースが存在しません。")),
            GitConnectionResult.Forbidden => this.ForbiddenError(),
            GitConnectionResult.Duplicate =>
                Conflict(new ErrorResponse("同じプロバイダ・アカウントの接続が既に存在します。")),
            _ => Created($"/api/git-connections/{outcome.Data!.Id}", outcome.Data),
        };
    }
}
