namespace TaskManagementSystem.Api.Dtos.Comments;

public record CommentCreatedDto(
    long Id,
    long TaskId,
    long UserId,
    string Comment,
    DateTime CreatedAt
);
