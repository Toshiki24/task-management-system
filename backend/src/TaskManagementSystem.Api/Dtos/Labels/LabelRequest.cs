using System.ComponentModel.DataAnnotations;

namespace TaskManagementSystem.Api.Dtos.Labels;

public record LabelRequest(
    [Required(ErrorMessage = "ラベル名は必須です。")]
    [MaxLength(50, ErrorMessage = "ラベル名は50文字以内で入力してください。")]
    string Name,

    [MaxLength(20, ErrorMessage = "色は20文字以内で入力してください。")]
    string? Color
);
