using TaskManagementSystem.Api.Dtos.Comments;

namespace TaskManagementSystem.Api.Services;

public enum CreateCommentResult
{
    Success,
    TaskNotFound,
    Forbidden,
}

public record CreateCommentOutcome(CreateCommentResult Result, CommentCreatedDto? Data = null);

public interface ICommentService
{
    Task<List<CommentDto>?> GetByTaskAsync(long taskId, long currentUserId);
    Task<CreateCommentOutcome> CreateAsync(long taskId, long userId, CommentRequest request);
}
