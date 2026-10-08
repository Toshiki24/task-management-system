using Microsoft.AspNetCore.Mvc;
using TaskManagementSystem.Api.Dtos.Common;
using TaskManagementSystem.Api.Dtos.Notifications;
using TaskManagementSystem.Api.Dtos.Tasks;
using TaskManagementSystem.Api.Dtos.Workspaces;
using TaskManagementSystem.Api.Extensions;
using TaskManagementSystem.Api.Services;

namespace TaskManagementSystem.Api.Controllers;

/// <summary>
/// 自分(認証ユーザー)に紐づく横断的な取得の入口(Phase 2 M1 §6 / M2 §6 / M3 §6)。
/// </summary>
[ApiController]
[Route("api/me")]
public class MeController : ControllerBase
{
    private readonly IWorkspaceService _workspaceService;
    private readonly ITaskService _taskService;
    private readonly INotificationService _notificationService;

    public MeController(
        IWorkspaceService workspaceService, ITaskService taskService, INotificationService notificationService)
    {
        _workspaceService = workspaceService;
        _taskService = taskService;
        _notificationService = notificationService;
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

    /// <summary>自分宛のアプリ内通知一覧(新しい順・ページング。M3 §6)。</summary>
    [HttpGet("notifications")]
    public async Task<ActionResult<PagedResult<NotificationDto>>> GetNotifications([FromQuery] NotificationQuery query)
    {
        return Ok(await _notificationService.GetAsync(this.GetCurrentUserId(), query));
    }

    /// <summary>自分宛の未読通知件数(M3 §6)。</summary>
    [HttpGet("notifications/unread-count")]
    public async Task<ActionResult<UnreadCountDto>> GetUnreadCount()
    {
        return Ok(new UnreadCountDto(await _notificationService.GetUnreadCountAsync(this.GetCurrentUserId())));
    }

    /// <summary>通知 1 件を既読にする(M3 §6)。</summary>
    [HttpPost("notifications/{id}/read")]
    public async Task<IActionResult> MarkRead(long id)
    {
        var found = await _notificationService.MarkReadAsync(id, this.GetCurrentUserId());
        if (!found)
        {
            return NotFound(new ErrorResponse("指定された通知が存在しません。"));
        }

        return NoContent();
    }

    /// <summary>自分宛の未読をすべて既読にする(M3 §6)。</summary>
    [HttpPost("notifications/read-all")]
    public async Task<ActionResult<UnreadCountDto>> MarkAllRead()
    {
        var updated = await _notificationService.MarkAllReadAsync(this.GetCurrentUserId());
        return Ok(new UnreadCountDto(updated));
    }
}
