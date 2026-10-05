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
    public async Task<ActionResult<List<AuditLogDto>>> GetAuditLogs([FromQuery] int limit = 100)
    {
        var logs = await _systemAdminService.GetAuditLogsAsync(this.GetCurrentUserId(), limit);
        if (logs is null)
        {
            return this.ForbiddenError();
        }

        return Ok(logs);
    }
}
