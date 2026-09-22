using System.ComponentModel.DataAnnotations;
using TaskManagementSystem.Api.Models;

namespace TaskManagementSystem.Api.Dtos.Tasks;

public record TaskRequest(
    long? AssigneeId,

    [Required(ErrorMessage = "タスク名は必須です。")]
    [MaxLength(200, ErrorMessage = "タスク名は200文字以内で入力してください。")]
    string Title,

    string? Description,

    [AllowedValues(null, TaskItemStatus.Todo, TaskItemStatus.InProgress, TaskItemStatus.Done,
        ErrorMessage = "タスク状態の値が不正です。")]
    string? Status,

    [AllowedValues(null, TaskItemPriority.Low, TaskItemPriority.Medium, TaskItemPriority.High,
        ErrorMessage = "優先度の値が不正です。")]
    string? Priority,

    DateOnly? DueDate
);
