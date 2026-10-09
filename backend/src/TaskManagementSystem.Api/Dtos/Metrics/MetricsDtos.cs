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
