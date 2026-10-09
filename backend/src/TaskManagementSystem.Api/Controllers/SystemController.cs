using Microsoft.AspNetCore.Mvc;
using TaskManagementSystem.Api.Dtos.Common;
using TaskManagementSystem.Api.Dtos.System;
using TaskManagementSystem.Api.Extensions;
using TaskManagementSystem.Api.Services;

namespace TaskManagementSystem.Api.Controllers;

/// <summary>
/// インスタンス全体の管理(System Admin 専用。Phase 2 M1 §5)。
/// System Admin 権限の付与/剥奪と、監査ログの参照を提供する。
/// </summary>
[ApiController]
[Route("api/system")]
public class SystemController : ControllerBase
{
    private readonly ISystemAdminService _systemAdminService;

    public SystemController(ISystemAdminService systemAdminService)
    {
        _systemAdminService = systemAdminService;
    }

    [HttpGet("admins")]
    public async Task<ActionResult<List<SystemAdminDto>>> GetAdmins()
    {
        var admins = await _systemAdminService.GetAdminsAsync(this.GetCurrentUserId());
        if (admins is null)
        {
            return this.ForbiddenError();
        }

        return Ok(admins);
    }

    [HttpPost("admins/{userId}")]
    public async Task<IActionResult> Grant(long userId)
    {
        var result = await _systemAdminService.GrantAsync(userId, this.GetCurrentUserId());

        return result switch
        {
            GrantSystemAdminResult.Forbidden => this.ForbiddenError(),
            GrantSystemAdminResult.UserNotFound =>
                NotFound(new ErrorResponse("指定されたユーザーが存在しません。")),
            GrantSystemAdminResult.AlreadyAdmin =>
                Conflict(new ErrorResponse("指定されたユーザーは既に System Admin です。")),
            _ => NoContent(),
        };
    }

    [HttpDelete("admins/{userId}")]
    public async Task<IActionResult> Revoke(long userId)
    {
        var result = await _systemAdminService.RevokeAsync(userId, this.GetCurrentUserId());

        return result switch
        {
            RevokeSystemAdminResult.Forbidden => this.ForbiddenError(),
            RevokeSystemAdminResult.UserNotFound =>
                NotFound(new ErrorResponse("指定されたユーザーが存在しません。")),
            RevokeSystemAdminResult.NotAdmin =>
                Conflict(new ErrorResponse("指定されたユーザーは System Admin ではありません。")),
            RevokeSystemAdminResult.LastAdmin =>
                Conflict(new ErrorResponse("System Admin は少なくとも1人必要です。")),
            _ => NoContent(),
        };
    }

    [HttpGet("audit-logs")]
    public async Task<ActionResult<AuditLogPageDto>> GetAuditLogs(
        [FromQuery] long? actorUserId = null,
        [FromQuery] string? action = null,
        [FromQuery] string? targetType = null,
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        [FromQuery] int offset = 0,
        [FromQuery] int limit = 50)
    {
        var query = new AuditLogQuery(actorUserId, action, targetType, from, to, offset, limit);
        var logs = await _systemAdminService.GetAuditLogsAsync(this.GetCurrentUserId(), query);
        if (logs is null)
        {
            return this.ForbiddenError();
        }

        return Ok(logs);
    }

    [HttpGet("stats")]
    public async Task<ActionResult<SystemStatsDto>> GetStats()
    {
        var stats = await _systemAdminService.GetStatsAsync(this.GetCurrentUserId());
        if (stats is null)
        {
            return this.ForbiddenError();
        }

        return Ok(stats);
    }

    [HttpGet("workspaces")]
    public async Task<ActionResult<List<AdminWorkspaceDto>>> GetWorkspaces()
    {
        var workspaces = await _systemAdminService.GetWorkspacesAsync(this.GetCurrentUserId());
        if (workspaces is null)
        {
            return this.ForbiddenError();
        }

        return Ok(workspaces);
    }

    [HttpGet("users")]
    public async Task<ActionResult<List<AdminUserDto>>> GetUsers()
    {
        var users = await _systemAdminService.GetUsersAsync(this.GetCurrentUserId());
        if (users is null)
        {
            return this.ForbiddenError();
        }

        return Ok(users);
    }
}
