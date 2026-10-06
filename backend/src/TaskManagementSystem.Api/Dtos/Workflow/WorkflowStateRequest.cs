using System.ComponentModel.DataAnnotations;
using TaskManagementSystem.Api.Models;

namespace TaskManagementSystem.Api.Dtos.Workflow;

/// <summary>状態の新規作成。key は作成後は不変(タスクの status が参照するため)。</summary>
public record WorkflowStateCreateRequest(
    [Required(ErrorMessage = "状態キーは必須です。")]
    [MaxLength(50, ErrorMessage = "状態キーは50文字以内で入力してください。")]
    [RegularExpression("^[A-Z0-9_]+$", ErrorMessage = "状態キーは英大文字・数字・アンダースコアのみ使用できます。")]
    string Key,

    [Required(ErrorMessage = "状態名は必須です。")]
    [MaxLength(100, ErrorMessage = "状態名は100文字以内で入力してください。")]
    string Name,

    [Required(ErrorMessage = "カテゴリは必須です。")]
    [AllowedValues(
        WorkflowStateCategory.Backlog, WorkflowStateCategory.Todo, WorkflowStateCategory.InProgress,
        WorkflowStateCategory.Done, WorkflowStateCategory.Cancelled,
        ErrorMessage = "カテゴリの値が不正です。")]
    string Category,

    [MaxLength(20, ErrorMessage = "色は20文字以内で入力してください。")]
    string? Color
);

/// <summary>状態の更新。key は変更しない。Position を指定すると並び替える。</summary>
public record WorkflowStateUpdateRequest(
    [Required(ErrorMessage = "状態名は必須です。")]
    [MaxLength(100, ErrorMessage = "状態名は100文字以内で入力してください。")]
    string Name,

    [Required(ErrorMessage = "カテゴリは必須です。")]
    [AllowedValues(
        WorkflowStateCategory.Backlog, WorkflowStateCategory.Todo, WorkflowStateCategory.InProgress,
        WorkflowStateCategory.Done, WorkflowStateCategory.Cancelled,
        ErrorMessage = "カテゴリの値が不正です。")]
    string Category,

    [Range(0, int.MaxValue, ErrorMessage = "並び順の値が不正です。")]
    int? Position,

    [MaxLength(20, ErrorMessage = "色は20文字以内で入力してください。")]
    string? Color
);
