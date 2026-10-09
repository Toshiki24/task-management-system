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

/// <summary>監査ログ閲覧のフィルタ・ページング条件(すべて任意)。</summary>
public record AuditLogQuery(
    long? ActorUserId = null,
    string? Action = null,
    string? TargetType = null,
    DateTime? From = null,
    DateTime? To = null,
    int Offset = 0,
    int Limit = 50
);

public interface ISystemAdminService
{
    /// <summary>System Admin の一覧。呼び出し元が System Admin でなければ null(= 403)。</summary>
    Task<List<SystemAdminDto>?> GetAdminsAsync(long currentUserId);

    Task<GrantSystemAdminResult> GrantAsync(long targetUserId, long currentUserId);

    Task<RevokeSystemAdminResult> RevokeAsync(long targetUserId, long currentUserId);

    /// <summary>監査ログ(新しい順、フィルタ・ページング対応)。System Admin でなければ null(= 403)。</summary>
    Task<AuditLogPageDto?> GetAuditLogsAsync(long currentUserId, AuditLogQuery query);

    /// <summary>システム統計。System Admin でなければ null(= 403)。</summary>
    Task<SystemStatsDto?> GetStatsAsync(long currentUserId);

    /// <summary>全ワークスペース一覧(作成日の新しい順)。System Admin でなければ null(= 403)。</summary>
    Task<List<AdminWorkspaceDto>?> GetWorkspacesAsync(long currentUserId);

    /// <summary>全ユーザー一覧(作成日の新しい順)。System Admin でなければ null(= 403)。</summary>
    Task<List<AdminUserDto>?> GetUsersAsync(long currentUserId);
}
