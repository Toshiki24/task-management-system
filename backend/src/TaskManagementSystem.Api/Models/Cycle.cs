namespace TaskManagementSystem.Api.Models;

/// <summary>サイクル(スプリント)の状態(Phase 2 M3 §8)。</summary>
public static class CycleStatus
{
    public const string Planned = "PLANNED";
    public const string Active = "ACTIVE";
    public const string Closed = "CLOSED";
}

/// <summary>
/// サイクル(スプリント。Phase 2 M3 §8)。プロジェクト単位。タスクは cycle_id で所属し、
/// 未割り当て(NULL)はバックログ扱い。削除してもタスクは消さずバックログへ戻す(FK は SET NULL)。
/// </summary>
public class Cycle
{
    public long Id { get; set; }
    public long ProjectId { get; set; }
    public string Name { get; set; } = null!;
    public DateOnly? StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public string Status { get; set; } = CycleStatus.Planned;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public Project Project { get; set; } = null!;
}
