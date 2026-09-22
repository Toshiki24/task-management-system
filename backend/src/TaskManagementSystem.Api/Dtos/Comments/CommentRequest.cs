using System.ComponentModel.DataAnnotations;

namespace TaskManagementSystem.Api.Dtos.Comments;

public record CommentRequest(
    [Required(ErrorMessage = "コメントは必須です。")]
    [MaxLength(1000, ErrorMessage = "コメントは1000文字以内で入力してください。")]
    string Comment
);
