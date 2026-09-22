namespace TaskManagementSystem.Api.Dtos.Comments;

public record CommentDto(
    long Id,
    long TaskId,
    long UserId,
    string UserName,
    string Comment,
    DateTime CreatedAt
);
