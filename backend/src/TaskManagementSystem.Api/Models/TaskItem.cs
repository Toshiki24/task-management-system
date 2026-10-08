namespace TaskManagementSystem.Api.Models;

public static class TaskItemStatus
{
    public const string Todo = "TODO";
    public const string InProgress = "IN_PROGRESS";
    public const string Done = "DONE";
}

public static class TaskItemPriority
{
    public const string Low = "LOW";
    public const string Medium = "MEDIUM";
    public const string High = "HIGH";
}

/// <summary>
/// DBの "tasks" テーブルに対応するEntity。
/// System.Threading.Tasks.Task との名前衝突を避けるため TaskItem とする。
/// </summary>
public class TaskItem
{
    public long Id { get; set; }
    public long ProjectId { get; set; }
    public long? AssigneeId { get; set; }

    /// <summary>親タスク。NULL=親(トップレベル)。サブタスクは同一プロジェクト・1 階層のみ(M2 §7.1)。</summary>
    public long? ParentTaskId { get; set; }
    public string Title { get; set; } = null!;
    public string? Description { get; set; }
    public string Status { get; set; } = TaskItemStatus.Todo;
    public string Priority { get; set; } = TaskItemPriority.Medium;

    /// <summary>同一 (project_id, status) 内でのカンバン表示順。小さいほど上(M2 §4.2)。</summary>
    public double BoardPosition { get; set; }

    /// <summary>見積(ポイント)。任意(M2 §8.2)。</summary>
    public int? EstimatePoints { get; set; }

    /// <summary>所属サイクル。NULL=バックログ(M3 §8)。</summary>
    public long? CycleId { get; set; }

    /// <summary>所属マイルストーン。NULL=未割り当て(M3 §8)。</summary>
    public long? MilestoneId { get; set; }

    public DateOnly? DueDate { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public Project Project { get; set; } = null!;
    public User? Assignee { get; set; }
    public ICollection<TaskComment> Comments { get; set; } = new List<TaskComment>();
    public ICollection<TaskStatusHistory> StatusHistories { get; set; } = new List<TaskStatusHistory>();
    public ICollection<TaskLabel> TaskLabels { get; set; } = new List<TaskLabel>();
    public ICollection<TaskChecklistItem> ChecklistItems { get; set; } = new List<TaskChecklistItem>();

    public TaskItem? ParentTask { get; set; }
    public ICollection<TaskItem> Subtasks { get; set; } = new List<TaskItem>();

    public Cycle? Cycle { get; set; }
    public Milestone? Milestone { get; set; }
}
