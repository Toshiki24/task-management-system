namespace TaskManagementSystem.Api.Models;

/// <summary>
/// タスクのウォッチャー(フォロー。M3 §4)。ウォッチ中のタスクが更新されると通知を受ける(通知は step5)。
/// 担当者・コメント投稿者・被メンション者は自動でウォッチに追加される。
/// </summary>
public class TaskWatcher
{
    public long TaskId { get; set; }
    public long UserId { get; set; }
    public DateTime CreatedAt { get; set; }

    public TaskItem Task { get; set; } = null!;
    public User User { get; set; } = null!;
}
