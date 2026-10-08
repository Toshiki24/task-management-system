using Microsoft.AspNetCore.Mvc;
using TaskManagementSystem.Api.Dtos.Common;
using TaskManagementSystem.Api.Dtos.Tasks;
using TaskManagementSystem.Api.Dtos.Workspaces;
using TaskManagementSystem.Api.Extensions;
using TaskManagementSystem.Api.Services;

namespace TaskManagementSystem.Api.Controllers;

/// <summary>
/// 自分(認証ユーザー)に紐づく横断的な取得の入口(Phase 2 M1 §6 / M2 §6)。
/// </summary>
[ApiController]
[Route("api/me")]
public class MeController : ControllerBase
{
    private readonly IWorkspaceService _workspaceService;
    private readonly ITaskService _taskService;

    public MeController(IWorkspaceService workspaceService, ITaskService taskService)
    {
        _workspaceService = workspaceService;
        _taskService = taskService;
    }

    /// <summary>自分が所属するワークスペースの一覧(所属分のみ。他ワークスペースは開示しない)。</summary>
    [HttpGet("workspaces")]
    public async Task<ActionResult<List<WorkspaceDto>>> GetMyWorkspaces()
    {
        return Ok(await _workspaceService.GetMineAsync(this.GetCurrentUserId()));
    }

    /// <summary>自分の担当タスク(所属する全ワークスペース横断、ページング付き。M2 §6)。</summary>
    [HttpGet("tasks")]
    public async Task<ActionResult<PagedResult<MyTaskDto>>> GetMyTasks([FromQuery] MyTasksQuery query)
    {
        return Ok(await _taskService.GetMyTasksAsync(this.GetCurrentUserId(), query));
    }

    /// <summary>閲覧できるタスクの横断検索(コマンドパレット用。キーワード未指定なら最近更新順。M2 §5.5)。</summary>
    [HttpGet("search/tasks")]
    public async Task<ActionResult<List<TaskSearchResultDto>>> SearchTasks(
        [FromQuery] string? keyword, [FromQuery] int? limit)
    {
        return Ok(await _taskService.SearchVisibleTasksAsync(this.GetCurrentUserId(), keyword, limit ?? 20));
    }
}
