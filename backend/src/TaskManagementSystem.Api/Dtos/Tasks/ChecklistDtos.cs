using System.ComponentModel.DataAnnotations;

namespace TaskManagementSystem.Api.Dtos.Tasks;

public record ChecklistItemDto(long Id, long TaskId, string Content, bool IsDone, int Position);

/// <summary>チェックリスト項目の作成・更新。IsDone は更新時のみ意味を持つ。</summary>
public record ChecklistItemRequest(
    [Required(ErrorMessage = "内容は必須です。")]
    [MaxLength(500, ErrorMessage = "内容は500文字以内で入力してください。")]
    string Content,

    bool IsDone = false
);
