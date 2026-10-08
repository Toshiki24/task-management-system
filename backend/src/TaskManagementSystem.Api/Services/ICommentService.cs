using TaskManagementSystem.Api.Dtos.Comments;

namespace TaskManagementSystem.Api.Services;

public enum CreateCommentResult
{
    Success,
    TaskNotFound,
    Forbidden,
}

public enum UpdateCommentResult
{
    Success,
    NotFound,
    Forbidden,
}

public enum DeleteCommentResult
{
    Success,
    NotFound,
    Forbidden,
}

public record CreateCommentOutcome(CreateCommentResult Result, CommentCreatedDto? Data = null);

public record UpdateCommentOutcome(UpdateCommentResult Result, CommentDto? Data = null);

public interface ICommentService
{
    Task<List<CommentDto>?> GetByTaskAsync(long taskId, long currentUserId);
    Task<CreateCommentOutcome> CreateAsync(long taskId, long userId, CommentRequest request);
    Task<UpdateCommentOutcome> UpdateAsync(long taskId, long commentId, long currentUserId, CommentRequest request);
    Task<DeleteCommentResult> DeleteAsync(long taskId, long commentId, long currentUserId);
}
