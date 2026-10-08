using Microsoft.EntityFrameworkCore;
using TaskManagementSystem.Api.Data;
using TaskManagementSystem.Api.Models;

namespace TaskManagementSystem.Api.Services;

/// <summary>
/// 自動ウォッチの登録ヘルパー(M3 §4)。担当者・コメント投稿者・被メンション者をウォッチに追加する。
/// 既にウォッチ済みのユーザーは追加しない(冪等)。保存は呼び出し側の SaveChanges に委ねる。
/// </summary>
internal static class WatcherRecorder
{
    public static async Task EnsureWatchingAsync(AppDbContext dbContext, long taskId, IEnumerable<long> userIds)
    {
        var ids = userIds.Distinct().ToList();
        if (ids.Count == 0)
        {
            return;
        }

        var existing = await dbContext.TaskWatchers
            .Where(w => w.TaskId == taskId && ids.Contains(w.UserId))
            .Select(w => w.UserId)
            .ToListAsync();

        foreach (var userId in ids.Except(existing))
        {
            dbContext.TaskWatchers.Add(new TaskWatcher { TaskId = taskId, UserId = userId });
        }
    }
}
