using System.ComponentModel.DataAnnotations;

namespace TaskManagementSystem.Api.Dtos.Tasks;

/// <summary>
/// 複数タスクの一括更新(M2 §5.4 一括操作)。指定された項目だけを全対象タスクへ適用する。
/// status は未指定=変更なし。担当は SetAssignee=true のときだけ適用(AssigneeId=null で未割り当て)。
/// ラベルは付与(AddLabelIds)・除去(RemoveLabelIds)を加減算で適用する。
/// </summary>
public record BulkUpdateTasksRequest(
    [Required(ErrorMessage = "対象タスクは必須です。")]
    [MinLength(1, ErrorMessage = "対象タスクを1件以上指定してください。")]
    long[] TaskIds,

    string? Status = null,

    bool SetAssignee = false,
    long? AssigneeId = null,

    long[]? AddLabelIds = null,
    long[]? RemoveLabelIds = null
);

/// <summary>一括更新の結果(更新件数)。</summary>
public record BulkUpdateTasksResponse(int Updated);
