namespace TaskManagementSystem.Api.Models;

/// <summary>
/// 監査ログ(Phase 2 M1 基本設計 §5)。メンバー変更・プロジェクト削除・System Admin の横断アクセス等、
/// 重要操作を記録する。記録に失敗しても本処理は止めない方針(欠落は最小化)。
/// metadata には秘密情報(トークン等)を入れない。
/// </summary>
public class AuditLog
{
    public long Id { get; set; }

    /// <summary>操作の実行者。</summary>
    public long ActorUserId { get; set; }

    /// <summary>操作種別。例: <c>workspace.member.role_changed</c>。</summary>
    public string Action { get; set; } = null!;

    /// <summary>対象の種別。例: <c>workspace</c> / <c>project</c> / <c>user</c>。</summary>
    public string TargetType { get; set; } = null!;

    /// <summary>対象の ID。</summary>
    public long TargetId { get; set; }

    /// <summary>文脈のワークスペース(横断操作等では NULL 可)。</summary>
    public long? WorkspaceId { get; set; }

    /// <summary>変更前後などの付随情報(JSONB)。秘密情報は含めない。</summary>
    public string? Metadata { get; set; }

    public DateTime CreatedAt { get; set; }

    public User ActorUser { get; set; } = null!;
}
