using System.ComponentModel.DataAnnotations;

namespace TaskManagementSystem.Api.Dtos.Tasks;

/// <summary>カンバンでのカード移動(M2 §4.2)。</summary>
public record MoveTaskRequest(
    [Required(ErrorMessage = "移動先の状態は必須です。")]
    string ToStatus,

    /// <summary>この ID のカードの直前に差し込む。null なら列の末尾へ。</summary>
    long? BeforeTaskId
);
