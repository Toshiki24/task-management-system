using Microsoft.AspNetCore.Mvc;
using TaskManagementSystem.Api.Dtos.Common;
using TaskManagementSystem.Api.Dtos.Git;
using TaskManagementSystem.Api.Extensions;
using TaskManagementSystem.Api.Models;
using TaskManagementSystem.Api.Services;

namespace TaskManagementSystem.Api.Controllers;

[ApiController]
[Route("api/git-connections/{connectionId}")]
public class GitConnectionsController : ControllerBase
{
    private readonly IGitConnectionService _service;

    public GitConnectionsController(IGitConnectionService service)
    {
        _service = service;
    }

    [HttpPatch]
    public async Task<ActionResult<GitConnectionDto>> Update(long connectionId, UpdateGitConnectionRequest request)
    {
        var outcome = await _service.UpdateAsync(connectionId, request, this.GetCurrentUserId());

        return outcome.Result switch
        {
            GitConnectionResult.ConnectionNotFound =>
                NotFound(new ErrorResponse("指定された接続が存在しません。")),
            GitConnectionResult.Forbidden => this.ForbiddenError(),
            _ => Ok(outcome.Data),
        };
    }

    [HttpDelete]
    public async Task<IActionResult> Delete(long connectionId)
    {
        var result = await _service.DeleteAsync(connectionId, this.GetCurrentUserId());

        return result switch
        {
            GitConnectionResult.ConnectionNotFound =>
                NotFound(new ErrorResponse("指定された接続が存在しません。")),
            GitConnectionResult.Forbidden => this.ForbiddenError(),
            _ => NoContent(),
        };
    }

    [HttpPost("/api/git-connections/{connectionId}/test")]
    public async Task<IActionResult> Test(long connectionId)
    {
        var result = await _service.TestAsync(connectionId, this.GetCurrentUserId());

        return result switch
        {
            GitConnectionResult.ConnectionNotFound =>
                NotFound(new ErrorResponse("指定された接続が存在しません。")),
            GitConnectionResult.Forbidden => this.ForbiddenError(),
            GitConnectionResult.ProviderUnavailable =>
                BadRequest(new ErrorResponse("対応していないプロバイダです。")),
            GitConnectionResult.TestFailed =>
                Ok(new { status = GitConnectionStatus.Error }),
            _ => Ok(new { status = GitConnectionStatus.Active }),
        };
    }
}
