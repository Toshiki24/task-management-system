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

        // 削除済みも含めて時系列で返す(スレッドの連続性のため)。本文は削除済みなら伏せる(M3 §3)
        return await _dbContext.TaskComments
            .Where(c => c.TaskId == taskId)
            .OrderBy(c => c.Id)
            .Select(c => new CommentDto(
                c.Id,
                c.TaskId,
                c.UserId,
                c.User.Name,
                c.DeletedAt != null ? null : c.Comment,
                c.Edited,
                c.DeletedAt != null,
                c.CreatedAt))
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

    public async Task<UpdateCommentOutcome> UpdateAsync(
        long taskId, long commentId, long currentUserId, CommentRequest request)
    {
        var access = await _dbContext.ResolveTaskAccessAsync(taskId, currentUserId);
        if (access is null || !access.Value.CanView)
        {
            return new UpdateCommentOutcome(UpdateCommentResult.NotFound);
        }

        var comment = await _dbContext.TaskComments
            .FirstOrDefaultAsync(c => c.Id == commentId && c.TaskId == taskId && c.DeletedAt == null);
        if (comment is null)
        {
            return new UpdateCommentOutcome(UpdateCommentResult.NotFound);
        }

        // 編集は投稿者本人のみ(M3 §3)
        if (comment.UserId != currentUserId)
        {
            return new UpdateCommentOutcome(UpdateCommentResult.Forbidden);
        }

        comment.Comment = request.Comment;
        comment.Edited = true;
        // UpdatedAt は SaveChanges 時に自動更新される
        await _dbContext.SaveChangesAsync();

        var name = await _dbContext.Users.Where(u => u.Id == comment.UserId).Select(u => u.Name).FirstAsync();
        return new UpdateCommentOutcome(
            UpdateCommentResult.Success,
            new CommentDto(comment.Id, comment.TaskId, comment.UserId, name, comment.Comment, comment.Edited, false, comment.CreatedAt));
    }

    public async Task<DeleteCommentResult> DeleteAsync(long taskId, long commentId, long currentUserId)
    {
        var access = await _dbContext.ResolveTaskAccessAsync(taskId, currentUserId);
        if (access is null || !access.Value.CanView)
        {
            return DeleteCommentResult.NotFound;
        }

        var comment = await _dbContext.TaskComments
            .FirstOrDefaultAsync(c => c.Id == commentId && c.TaskId == taskId && c.DeletedAt == null);
        if (comment is null)
        {
            return DeleteCommentResult.NotFound;
        }

        // 削除は投稿者本人、またはプロジェクト管理者(Project OWNER / WS Admin)(M3 §3)
        if (comment.UserId != currentUserId && !access.Value.CanManageProject)
        {
            return DeleteCommentResult.Forbidden;
        }

        comment.DeletedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);
        await _dbContext.SaveChangesAsync();
        return DeleteCommentResult.Success;
    }
}
