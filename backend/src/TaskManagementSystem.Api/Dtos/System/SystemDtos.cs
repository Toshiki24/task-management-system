using System.ComponentModel.DataAnnotations;

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

/// <summary>管理コンソールの全ユーザー一覧の 1 件。</summary>
public record AdminUserDto(
    long Id,
    string Name,
    string Email,
    bool IsSystemAdmin,
    bool IsMfaEnabled,
    DateTime CreatedAt
);

/// <summary>
/// System Admin による手動ユーザー作成(M5 §8 / メール招待が使えない運用向け)。
/// メール・パスワードで作成し、指定ワークスペースにロール付きで追加する。名前は任意(未指定ならメール名を使う)。
/// </summary>
public record CreateManagedUserRequest(
    [Required(ErrorMessage = "メールアドレスは必須です。")]
    [EmailAddress(ErrorMessage = "メールアドレスの形式が正しくありません。")]
    [MaxLength(255, ErrorMessage = "メールアドレスは255文字以内で入力してください。")]
    string Email,

    [Required(ErrorMessage = "パスワードは必須です。")]
    [MinLength(8, ErrorMessage = "パスワードは8文字以上で入力してください。")]
    [MaxLength(200, ErrorMessage = "パスワードは200文字以内で入力してください。")]
    string Password,

    [MaxLength(100, ErrorMessage = "名前は100文字以内で入力してください。")]
    string? Name,

    [Required(ErrorMessage = "ワークスペースは必須です。")]
    long WorkspaceId,

    [Required(ErrorMessage = "ロールは必須です。")]
    [AllowedValues("ADMIN", "MEMBER", "VIEWER", ErrorMessage = "ロールが不正です。")]
    string Role
);
