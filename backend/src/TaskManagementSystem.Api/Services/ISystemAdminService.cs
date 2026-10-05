using TaskManagementSystem.Api.Dtos.System;

namespace TaskManagementSystem.Api.Services;

public enum GrantSystemAdminResult
{
    Success,
    Forbidden,
    UserNotFound,
    AlreadyAdmin,
}

public enum RevokeSystemAdminResult
{
    Success,
    Forbidden,
    UserNotFound,
    NotAdmin,
    LastAdmin,
}

public interface ISystemAdminService
{
    /// <summary>System Admin の一覧。呼び出し元が System Admin でなければ null(= 403)。</summary>
    Task<List<SystemAdminDto>?> GetAdminsAsync(long currentUserId);

    Task<GrantSystemAdminResult> GrantAsync(long targetUserId, long currentUserId);

    Task<RevokeSystemAdminResult> RevokeAsync(long targetUserId, long currentUserId);

    /// <summary>監査ログ(新しい順)。呼び出し元が System Admin でなければ null(= 403)。</summary>
    Task<List<AuditLogDto>?> GetAuditLogsAsync(long currentUserId, int limit);
}
