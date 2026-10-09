namespace TaskManagementSystem.Api.Dtos.Metrics;

/// <summary>状態別の件数(M5 §2)。</summary>
public record StatusCountDto(string Key, string Name, string Category, int Count);

/// <summary>担当別の負荷(未完了の件数・見積合計)。AssigneeId=null は未割り当て。</summary>
public record AssigneeLoadDto(long? AssigneeId, string Name, int OpenCount, int EstimatePoints);

/// <summary>プロジェクト/ワークスペースの基本指標(M5 §2)。</summary>
public record MetricsDto(
    int Total,
    int DoneCount,
    double CompletionRate,
    int OverdueCount,
    IReadOnlyList<StatusCountDto> StatusCounts,
    IReadOnlyList<AssigneeLoadDto> AssigneeLoads);

/// <summary>日次のスループット(その日に完了へ遷移した件数)。</summary>
public record ThroughputPointDto(DateOnly Date, int Count);

/// <summary>
/// 開発指標(M5 §2)。状態履歴から算出する。サンプルが無ければ平均は null。
/// サイクルタイム=初めて IN_PROGRESS→初めて DONE、リードタイム=作成→初めて DONE。
/// </summary>
public record DevMetricsDto(
    int Days,
    double? AvgCycleTimeHours,
    double? AvgLeadTimeHours,
    int CompletedInPeriod,
    IReadOnlyList<ThroughputPointDto> Throughput);
