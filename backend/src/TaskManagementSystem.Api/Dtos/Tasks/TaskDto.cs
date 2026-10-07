using TaskManagementSystem.Api.Dtos.Labels;

namespace TaskManagementSystem.Api.Dtos.Tasks;

/// <summary>サブタスクの進捗(完了数/全数)。完了は状態カテゴリが DONE/CANCELLED のもの(M2 §7.1)。</summary>
public record SubtaskProgress(int Done, int Total);

public record TaskDto(
    long Id,
    long ProjectId,
    long? AssigneeId,
    string Title,
    string? Description,
    string Status,
    string Priority,
    DateOnly? DueDate,
    double BoardPosition,
    int? EstimatePoints,
    IReadOnlyList<LabelDto> Labels,
    long? ParentTaskId,
    SubtaskProgress SubtaskProgress
);
