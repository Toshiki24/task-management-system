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
        if (await _dbContext.GetTaskProjectRoleAsync(taskId, currentUserId) is null)
        {
            return null;
        }

        return await _dbContext.TaskComments
            .Where(c => c.TaskId == taskId)
            .OrderBy(c => c.Id)
            .Select(c => new CommentDto(c.Id, c.TaskId, c.UserId, c.User.Name, c.Comment, c.CreatedAt))
            .ToListAsync();
    }

    public async Task<CommentCreatedDto?> CreateAsync(long taskId, long userId, CommentRequest request)
    {
        // 投稿者(=ログイン中のユーザー)がタスクのプロジェクトに所属している場合のみ投稿できる
        if (await _dbContext.GetTaskProjectRoleAsync(taskId, userId) is null)
        {
            return null;
        }

        var comment = new TaskComment
        {
            TaskId = taskId,
            UserId = userId,
            Comment = request.Comment,
        };

        _dbContext.TaskComments.Add(comment);
        await _dbContext.SaveChangesAsync();

        return new CommentCreatedDto(comment.Id, comment.TaskId, comment.UserId, comment.Comment, comment.CreatedAt);
    }
}
