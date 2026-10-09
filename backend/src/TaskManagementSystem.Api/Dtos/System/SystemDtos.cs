namespace TaskManagementSystem.Api.Dtos.System;

/// <summary>System Admin 権限を持つユーザー。</summary>
public record SystemAdminDto(long Id, string Name, string Email);

/// <summary>監査ログの 1 件。<paramref name="Metadata"/> は JSON 文字列(無い場合は null)。</summary>
public record AuditLogDto(
    long Id,
    long ActorUserId,
    string? ActorName,
    string Action,
    string TargetType,
    long TargetId,
    long? WorkspaceId,
    string? Metadata,
    DateTime CreatedAt
);

/// <summary>監査ログのページング結果(新しい順)。<paramref name="Total"/> はフィルタ適用後の総件数。</summary>
public record AuditLogPageDto(IReadOnlyList<AuditLogDto> Items, int Total);

/// <summary>管理コンソールのシステム統計(System Admin 専用)。</summary>
public record SystemStatsDto(
    int WorkspaceCount,
    int ProjectCount,
    int TaskCount,
    int UserCount,
    int SystemAdminCount,
    int RecentActivityCount
);

/// <summary>管理コンソールの全ワークスペース一覧の 1 件。</summary>
public record AdminWorkspaceDto(
    long Id,
    string Name,
    int MemberCount,
    int ProjectCount,
    bool IsArchived,
    DateTime CreatedAt
);

/// <summary>管理コンソールの全ユーザー一覧の 1 件。MFA は M5 §6 で追加予定。</summary>
public record AdminUserDto(
    long Id,
    string Name,
    string Email,
    bool IsSystemAdmin,
    DateTime CreatedAt
);
