using Microsoft.EntityFrameworkCore;
using TaskManagementSystem.Api.Data;
using TaskManagementSystem.Api.Dtos.Tasks;
using TaskManagementSystem.Api.Models;

namespace TaskManagementSystem.Api.Services;

/// <summary>
/// タスク内チェックリストの管理(Phase 2 M2 §7.2)。参照は CanView、変更は CanWrite(Viewer 不可)。
/// 認可は M1 の ProjectAccess(ResolveTaskAccessAsync)に集約。すべて LINQ/EF。
/// </summary>
public class ChecklistService : IChecklistService
{
    private readonly AppDbContext _dbContext;

    public ChecklistService(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<List<ChecklistItemDto>?> GetByTaskAsync(long taskId, long currentUserId)
    {
        var access = await _dbContext.ResolveTaskAccessAsync(taskId, currentUserId);
        if (access is null || !access.Value.CanView)
        {
            return null;
        }

        return await _dbContext.TaskChecklistItems
            .Where(i => i.TaskId == taskId)
            .OrderBy(i => i.Position)
            .ThenBy(i => i.Id)
            .Select(i => new ChecklistItemDto(i.Id, i.TaskId, i.Content, i.IsDone, i.Position))
            .ToListAsync();
    }

    public async Task<CreateChecklistItemOutcome> CreateAsync(
        long taskId, ChecklistItemRequest request, long currentUserId)
    {
        var access = await _dbContext.ResolveTaskAccessAsync(taskId, currentUserId);
        if (access is null || !access.Value.CanView)
        {
            return new CreateChecklistItemOutcome(CreateChecklistItemResult.TaskNotFound);
        }

        if (!access.Value.CanWrite)
        {
            return new CreateChecklistItemOutcome(CreateChecklistItemResult.Forbidden);
        }

        var maxPosition = await _dbContext.TaskChecklistItems
            .Where(i => i.TaskId == taskId)
            .Select(i => (int?)i.Position)
            .MaxAsync();

        var item = new TaskChecklistItem
        {
            TaskId = taskId,
            Content = request.Content,
            IsDone = request.IsDone,
            Position = (maxPosition ?? -1) + 1,
        };
        _dbContext.TaskChecklistItems.Add(item);
        await _dbContext.SaveChangesAsync();

        return new CreateChecklistItemOutcome(
            CreateChecklistItemResult.Success,
            new ChecklistItemDto(item.Id, item.TaskId, item.Content, item.IsDone, item.Position));
    }

    public async Task<UpdateChecklistItemOutcome> UpdateAsync(
        long taskId, long itemId, ChecklistItemRequest request, long currentUserId)
    {
        var access = await _dbContext.ResolveTaskAccessAsync(taskId, currentUserId);
        if (access is null || !access.Value.CanView)
        {
            return new UpdateChecklistItemOutcome(UpdateChecklistItemResult.NotFound);
        }

        if (!access.Value.CanWrite)
        {
            return new UpdateChecklistItemOutcome(UpdateChecklistItemResult.Forbidden);
        }

        var item = await _dbContext.TaskChecklistItems
            .FirstOrDefaultAsync(i => i.Id == itemId && i.TaskId == taskId);
        if (item is null)
        {
            return new UpdateChecklistItemOutcome(UpdateChecklistItemResult.NotFound);
        }

        item.Content = request.Content;
        item.IsDone = request.IsDone;
        await _dbContext.SaveChangesAsync();

        return new UpdateChecklistItemOutcome(
            UpdateChecklistItemResult.Success,
            new ChecklistItemDto(item.Id, item.TaskId, item.Content, item.IsDone, item.Position));
    }

    public async Task<DeleteChecklistItemResult> DeleteAsync(long taskId, long itemId, long currentUserId)
    {
        var access = await _dbContext.ResolveTaskAccessAsync(taskId, currentUserId);
        if (access is null || !access.Value.CanView)
        {
            return DeleteChecklistItemResult.NotFound;
        }

        if (!access.Value.CanWrite)
        {
            return DeleteChecklistItemResult.Forbidden;
        }

        var item = await _dbContext.TaskChecklistItems
            .FirstOrDefaultAsync(i => i.Id == itemId && i.TaskId == taskId);
        if (item is null)
        {
            return DeleteChecklistItemResult.NotFound;
        }

        _dbContext.TaskChecklistItems.Remove(item);
        await _dbContext.SaveChangesAsync();
        return DeleteChecklistItemResult.Success;
    }
}
