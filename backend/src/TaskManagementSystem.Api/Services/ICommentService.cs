using TaskManagementSystem.Api.Dtos.Comments;

namespace TaskManagementSystem.Api.Services;

public interface ICommentService
{
    Task<List<CommentDto>?> GetByTaskAsync(long taskId);
    Task<CommentCreatedDto?> CreateAsync(long taskId, long userId, CommentRequest request);
}
