using System.ComponentModel.DataAnnotations;
using TaskManagementSystem.Api.Models;

namespace TaskManagementSystem.Api.Dtos.Projects;

public record ProjectRequest(
    [Required(ErrorMessage = "プロジェクト名は必須です。")]
    [MaxLength(200, ErrorMessage = "プロジェクト名は200文字以内で入力してください。")]
    string Name,

    string? Description,

    [AllowedValues(null, ProjectStatus.Active, ProjectStatus.Completed, ProjectStatus.Archived,
        ErrorMessage = "プロジェクト状態の値が不正です。")]
    string? Status,

    DateOnly? StartDate,

    DateOnly? EndDate
);
