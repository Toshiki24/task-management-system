namespace TaskManagementSystem.Api.Models;

/// <summary>マイルストーン/リリースの状態(Phase 2 M3 §8)。</summary>
public static class MilestoneStatus
{
    public const string Open = "OPEN";
    public const string Closed = "CLOSED";
}

/// <summary>
/// マイルストーン/リリース(Phase 2 M3 §8)。プロジェクト単位で期日(due_date)を持ち、
/// タスクは milestone_id で束ねる。未割り当て(NULL)は対象外。削除してもタスクは消さず
/// 未割り当てへ戻す(FK は SET NULL)。
/// </summary>
public class Milestone
{
    public long Id { get; set; }
    public long ProjectId { get; set; }
    public string Name { get; set; } = null!;
    public DateOnly? DueDate { get; set; }
    public string Status { get; set; } = MilestoneStatus.Open;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public Project Project { get; set; } = null!;
}
