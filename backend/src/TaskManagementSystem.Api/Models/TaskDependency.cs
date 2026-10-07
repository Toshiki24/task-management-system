namespace TaskManagementSystem.Api.Models;

/// <summary>
/// タスク間の依存関係(ブロック/被ブロック。Phase 2 M2 §7.3)。
/// 「BlockingTask が終わるまで BlockedTask は進められない」を表す有向辺。
/// 同一プロジェクト内・自己参照不可・循環不可(妥当性はサービス層で LINQ により検証)。
/// </summary>
public class TaskDependency
{
    public long Id { get; set; }

    /// <summary>ブロックする側(先に完了すべきタスク)。</summary>
    public long BlockingTaskId { get; set; }

    /// <summary>ブロックされる側(待たされるタスク)。</summary>
    public long BlockedTaskId { get; set; }

    public DateTime CreatedAt { get; set; }

    public TaskItem BlockingTask { get; set; } = null!;
    public TaskItem BlockedTask { get; set; } = null!;
}
