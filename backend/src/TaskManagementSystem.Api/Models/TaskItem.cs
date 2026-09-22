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
    public string Title { get; set; } = null!;
    public string? Description { get; set; }
    public string Status { get; set; } = TaskItemStatus.Todo;
    public string Priority { get; set; } = TaskItemPriority.Medium;
    public DateOnly? DueDate { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public Project Project { get; set; } = null!;
    public User? Assignee { get; set; }
    public ICollection<TaskComment> Comments { get; set; } = new List<TaskComment>();
    public ICollection<TaskStatusHistory> StatusHistories { get; set; } = new List<TaskStatusHistory>();
}
