using Microsoft.EntityFrameworkCore;
using TaskManagementSystem.Api.Data;
using TaskManagementSystem.Api.Dtos.Comments;
using TaskManagementSystem.Api.Models;

namespace TaskManagementSystem.Api.Services;

public class CommentService : ICommentService
{
    private readonly AppDbContext _dbContext;

    public CommentService(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<List<CommentDto>?> GetByTaskAsync(long taskId, long currentUserId)
    {
        var access = await _dbContext.ResolveTaskAccessAsync(taskId, currentUserId);
        if (access is null || !access.Value.CanView)
        {
            return null;
        }

        return await _dbContext.TaskComments
            .Where(c => c.TaskId == taskId)
            .OrderBy(c => c.Id)
            .Select(c => new CommentDto(c.Id, c.TaskId, c.UserId, c.User.Name, c.Comment, c.CreatedAt))
            .ToListAsync();
    }

    public async Task<CreateCommentOutcome> CreateAsync(long taskId, long userId, CommentRequest request)
    {
        var access = await _dbContext.ResolveTaskAccessAsync(taskId, userId);
        if (access is null || !access.Value.CanView)
        {
            return new CreateCommentOutcome(CreateCommentResult.TaskNotFound);
        }

        // Viewer はコメントを投稿できない
        if (!access.Value.CanWrite)
        {
            return new CreateCommentOutcome(CreateCommentResult.Forbidden);
        }

        var comment = new TaskComment
        {
            TaskId = taskId,
            UserId = userId,
            Comment = request.Comment,
        };

        _dbContext.TaskComments.Add(comment);
        await _dbContext.SaveChangesAsync();

        // コメント投稿をアクティビティに記録する(M3 §5)
        var projectId = await _dbContext.Tasks
            .Where(t => t.Id == taskId).Select(t => t.ProjectId).FirstAsync();
        var excerpt = request.Comment.Length > 50 ? request.Comment[..50] : request.Comment;
        ActivityRecorder.Record(_dbContext, projectId, taskId, userId,
            ActivityVerb.Commented, new { commentId = comment.Id, excerpt });
        await _dbContext.SaveChangesAsync();

        return new CreateCommentOutcome(
            CreateCommentResult.Success,
            new CommentCreatedDto(comment.Id, comment.TaskId, comment.UserId, comment.Comment, comment.CreatedAt));
    }
}
