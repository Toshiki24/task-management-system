namespace TaskManagementSystem.Api.Models;

/// <summary>
/// Phase 2機能。MVPでは書き込みを行わないが、テーブル自体は先に定義する。
/// </summary>
public class TaskStatusHistory
{
    public long Id { get; set; }
    public long TaskId { get; set; }
    public long ChangedBy { get; set; }
    public string? FromStatus { get; set; }
    public string ToStatus { get; set; } = null!;
    public DateTime CreatedAt { get; set; }

    public TaskItem Task { get; set; } = null!;
    public User ChangedByUser { get; set; } = null!;
}
