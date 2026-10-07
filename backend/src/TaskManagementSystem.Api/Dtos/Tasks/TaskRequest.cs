using System.ComponentModel.DataAnnotations;
using TaskManagementSystem.Api.Models;

namespace TaskManagementSystem.Api.Dtos.Tasks;

public record TaskRequest(
    long? AssigneeId,

    [Required(ErrorMessage = "タスク名は必須です。")]
    [MaxLength(200, ErrorMessage = "タスク名は200文字以内で入力してください。")]
    string Title,

    string? Description,

    // status はワークスペースのワークフロー(workflow_states)に対して検証するため、
    // ここでは固定値の AllowedValues を使わない(検証は TaskService 側で LINQ により行う。M2 §3.2)。
    string? Status,

    [AllowedValues(null, TaskItemPriority.Low, TaskItemPriority.Medium, TaskItemPriority.High,
        ErrorMessage = "優先度の値が不正です。")]
    string? Priority,

    DateOnly? DueDate,

    [Range(0, 10000, ErrorMessage = "見積の値が不正です。")]
    int? EstimatePoints = null,

    /// <summary>付与するラベル ID の集合(完全上書き)。null は変更なし、空配列は全解除。</summary>
    long[]? LabelIds = null
);
