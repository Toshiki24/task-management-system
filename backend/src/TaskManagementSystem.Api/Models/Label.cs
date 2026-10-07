namespace TaskManagementSystem.Api.Models;

/// <summary>
/// ラベル(ワークスペース単位)。タスクに複数付与できる(Phase 2 M2 §8.1)。
/// </summary>
public class Label
{
    public long Id { get; set; }
    public long WorkspaceId { get; set; }
    public string Name { get; set; } = null!;

    /// <summary>表示色(任意)。</summary>
    public string? Color { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public Workspace Workspace { get; set; } = null!;
    public ICollection<TaskLabel> TaskLabels { get; set; } = new List<TaskLabel>();
}

/// <summary>タスクとラベルの多対多(Phase 2 M2 §8.1)。</summary>
public class TaskLabel
{
    public long TaskId { get; set; }
    public long LabelId { get; set; }

    public TaskItem Task { get; set; } = null!;
    public Label Label { get; set; } = null!;
}
