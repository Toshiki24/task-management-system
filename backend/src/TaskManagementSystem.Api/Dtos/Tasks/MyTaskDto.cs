using TaskManagementSystem.Api.Dtos.Labels;

namespace TaskManagementSystem.Api.Dtos.Tasks;

/// <summary>My Tasks(自分の担当タスク)横断一覧の1件。どのWS/プロジェクトのタスクかが分かるよう文脈を含む(M2 §6)。</summary>
public record MyTaskDto(
    long Id,
    long ProjectId,
    string ProjectName,
    long WorkspaceId,
    string WorkspaceName,
    string Title,
    string Status,
    string Priority,
    DateOnly? DueDate,
    int? EstimatePoints,
    IReadOnlyList<LabelDto> Labels
);

/// <summary>My Tasks の絞り込み・並べ替え・ページング条件(M2 §6)。担当者は常に自分。</summary>
public class MyTasksQuery
{
    public string[]? Status { get; set; }
    public string[]? Priority { get; set; }
    public string? Keyword { get; set; }

    /// <summary>並べ替え。未指定なら期限の近い順(null は末尾)。</summary>
    public string? Sort { get; set; }

    public int? Page { get; set; }
    public int? PageSize { get; set; }
}
