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

    public async Task<List<CommentDto>?> GetByTaskAsync(long taskId)
    {
        var taskExists = await _dbContext.Tasks.AnyAsync(t => t.Id == taskId);
        if (!taskExists)
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
        var taskExists = await _dbContext.Tasks.AnyAsync(t => t.Id == taskId);
        if (!taskExists)
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
