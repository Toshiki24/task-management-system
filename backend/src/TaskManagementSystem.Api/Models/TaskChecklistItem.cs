namespace TaskManagementSystem.Api.Models;

/// <summary>
/// タスク内のチェックリスト項目(Phase 2 M2 §7.2)。サブタスク(独立タスク)とは別物で、
/// 担当者や期限は持たない軽量な ToDo。
/// </summary>
public class TaskChecklistItem
{
    public long Id { get; set; }
    public long TaskId { get; set; }
    public string Content { get; set; } = null!;
    public bool IsDone { get; set; }
    public int Position { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public TaskItem Task { get; set; } = null!;
}
