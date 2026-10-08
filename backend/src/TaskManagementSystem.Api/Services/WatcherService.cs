using Microsoft.EntityFrameworkCore;
using TaskManagementSystem.Api.Data;
using TaskManagementSystem.Api.Dtos.Tasks;
using TaskManagementSystem.Api.Models;

namespace TaskManagementSystem.Api.Services;

/// <summary>
/// タスクのウォッチャー(フォロー。M3 §4)。参照・登録・解除。可視性は M1 の ResolveTaskAccess に集約。
/// 自動ウォッチ(担当・コメント・メンション)は各サービスが WatcherRecorder 経由で行う。すべて LINQ/EF。
/// </summary>
public class WatcherService : IWatcherService
{
    private readonly AppDbContext _dbContext;

    public WatcherService(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<WatchersDto?> GetWatchersAsync(long taskId, long currentUserId)
    {
        var access = await _dbContext.ResolveTaskAccessAsync(taskId, currentUserId);
        if (access is null || !access.Value.CanView)
        {
            return null;
        }

        var watchers = await _dbContext.TaskWatchers
            .Where(w => w.TaskId == taskId)
            .OrderBy(w => w.User.Name)
            .Select(w => new WatcherDto(w.UserId, w.User.Name))
            .ToListAsync();

        return new WatchersDto(watchers, watchers.Any(w => w.UserId == currentUserId));
    }

    public async Task<WatchResult> WatchAsync(long taskId, long currentUserId)
    {
        var access = await _dbContext.ResolveTaskAccessAsync(taskId, currentUserId);
        if (access is null || !access.Value.CanView)
        {
            return WatchResult.TaskNotFound;
        }

        var already = await _dbContext.TaskWatchers
            .AnyAsync(w => w.TaskId == taskId && w.UserId == currentUserId);
        if (!already)
        {
            _dbContext.TaskWatchers.Add(new TaskWatcher { TaskId = taskId, UserId = currentUserId });
            await _dbContext.SaveChangesAsync();
        }

        return WatchResult.Success;
    }

    public async Task<WatchResult> UnwatchAsync(long taskId, long currentUserId)
    {
        var access = await _dbContext.ResolveTaskAccessAsync(taskId, currentUserId);
        if (access is null || !access.Value.CanView)
        {
            return WatchResult.TaskNotFound;
        }

        var watcher = await _dbContext.TaskWatchers
            .FirstOrDefaultAsync(w => w.TaskId == taskId && w.UserId == currentUserId);
        if (watcher is not null)
        {
            _dbContext.TaskWatchers.Remove(watcher);
            await _dbContext.SaveChangesAsync();
        }

        return WatchResult.Success;
    }
}
