using Microsoft.AspNetCore.Mvc;
using TaskManagementSystem.Api.Dtos.Workspaces;
using TaskManagementSystem.Api.Extensions;
using TaskManagementSystem.Api.Services;

namespace TaskManagementSystem.Api.Controllers;

/// <summary>
/// 自分(認証ユーザー)に紐づく横断的な取得の入口(Phase 2 M1 §6)。
/// </summary>
[ApiController]
[Route("api/me")]
public class MeController : ControllerBase
{
    private readonly IWorkspaceService _workspaceService;

    public MeController(IWorkspaceService workspaceService)
    {
        _workspaceService = workspaceService;
    }

    /// <summary>自分が所属するワークスペースの一覧(所属分のみ。他ワークスペースは開示しない)。</summary>
    [HttpGet("workspaces")]
    public async Task<ActionResult<List<WorkspaceDto>>> GetMyWorkspaces()
    {
        return Ok(await _workspaceService.GetMineAsync(this.GetCurrentUserId()));
    }
}
