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
