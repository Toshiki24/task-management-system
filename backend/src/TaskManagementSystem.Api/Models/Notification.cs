namespace TaskManagementSystem.Api.Models;

/// <summary>通知の種別(Phase 2 M3 §6)。</summary>
public static class NotificationType
{
    public const string Mention = "MENTION";
    public const string Assigned = "ASSIGNED";
    public const string StatusChanged = "STATUS_CHANGED";
    public const string Comment = "COMMENT";
    public const string DueSoon = "DUE_SOON";
    public const string DueOverdue = "DUE_OVERDUE";
}

/// <summary>
/// ユーザー宛のアプリ内通知(Phase 2 M3 §6)。メンション・担当・状態変更・コメント等で生成する。
/// 対象タスク・実行者は削除されても通知履歴は残す(FK は SET NULL)。
/// </summary>
public class Notification
{
    public long Id { get; set; }
    public long RecipientUserId { get; set; }
    public string Type { get; set; } = null!;
    public long? TaskId { get; set; }
    public long? ActorUserId { get; set; }

    /// <summary>種別ごとの付加情報(JSON)。例: STATUS_CHANGED={from,to}、COMMENT/MENTION={commentId}。</summary>
    public string? Payload { get; set; }

    public bool IsRead { get; set; }
    public DateTime? ReadAt { get; set; }
    public DateTime CreatedAt { get; set; }

    public User RecipientUser { get; set; } = null!;
    public TaskItem? Task { get; set; }
    public User? ActorUser { get; set; }
}
