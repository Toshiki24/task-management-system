using Microsoft.EntityFrameworkCore;
using TaskManagementSystem.Api.Data;
using TaskManagementSystem.Api.Models;

namespace TaskManagementSystem.Api.Services;

/// <summary>
/// 期限通知の生成(M3 §6/§7)。日次のスケジュール実行から呼ばれる想定。すべて LINQ/EF。
/// 対象は「期限あり・未完(状態カテゴリが DONE/CANCELLED でない)」タスク。
/// 受信者は担当者＋ウォッチャー。同一日・同一種別の重複は作らない。
/// </summary>
public class DueNotificationService : IDueNotificationService
{
    private readonly AppDbContext _dbContext;

    public DueNotificationService(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<int> GenerateAsync(DateOnly today, int dueSoonWithinDays)
    {
        var within = dueSoonWithinDays >= 0 ? dueSoonWithinDays : 0;
        var soonLimit = today.AddDays(within);

        // 期限あり・未完(その WS のワークフローで DONE/CANCELLED カテゴリに該当しない)タスク
        var candidates = await _dbContext.Tasks
            .Where(t => t.DueDate != null)
            .Where(t => !_dbContext.WorkflowStates.Any(s =>
                s.WorkspaceId == t.Project.WorkspaceId
                && s.Key == t.Status
                && (s.Category == WorkflowStateCategory.Done || s.Category == WorkflowStateCategory.Cancelled)))
            .Select(t => new { t.Id, t.AssigneeId, DueDate = t.DueDate!.Value })
            .ToListAsync();

        // 期限超過(< today)または期限間近(today 〜 today+within)のみ対象にし、種別を決める
        var targets = candidates
            .Select(t => new
            {
                t.Id,
                t.AssigneeId,
                Type = t.DueDate < today
                    ? NotificationType.DueOverdue
                    : t.DueDate <= soonLimit ? NotificationType.DueSoon : null,
                t.DueDate,
            })
            .Where(t => t.Type is not null)
            .ToList();

        if (targets.Count == 0)
        {
            return 0;
        }

        var taskIds = targets.Select(t => t.Id).ToList();

        // 受信者: 担当者 ∪ ウォッチャー(タスクごと)
        var watchers = await _dbContext.TaskWatchers
            .Where(w => taskIds.Contains(w.TaskId))
            .Select(w => new { w.TaskId, w.UserId })
            .ToListAsync();
        var watchersByTask = watchers
            .GroupBy(w => w.TaskId)
            .ToDictionary(g => g.Key, g => g.Select(w => w.UserId).ToList());

        // 重複防止: today 00:00(UTC)以降に作られた DUE_* 通知を既存として扱う
        var sinceUtc = DateTime.SpecifyKind(today.ToDateTime(TimeOnly.MinValue), DateTimeKind.Unspecified);
        var existing = await _dbContext.Notifications
            .Where(n => taskIds.Contains(n.TaskId!.Value)
                && (n.Type == NotificationType.DueSoon || n.Type == NotificationType.DueOverdue)
                && n.CreatedAt >= sinceUtc)
            .Select(n => new { n.RecipientUserId, TaskId = n.TaskId!.Value, n.Type })
            .ToListAsync();
        var existingKeys = existing
            .Select(e => (e.RecipientUserId, e.TaskId, e.Type))
            .ToHashSet();

        var created = 0;
        foreach (var target in targets)
        {
            var recipients = new HashSet<long>();
            if (target.AssigneeId is { } assignee)
            {
                recipients.Add(assignee);
            }

            if (watchersByTask.TryGetValue(target.Id, out var watcherIds))
            {
                foreach (var id in watcherIds)
                {
                    recipients.Add(id);
                }
            }

            foreach (var recipientId in recipients)
            {
                if (!existingKeys.Add((recipientId, target.Id, target.Type!)))
                {
                    continue; // 同一日・同一種別の重複
                }

                _dbContext.Notifications.Add(new Notification
                {
                    RecipientUserId = recipientId,
                    Type = target.Type!,
                    TaskId = target.Id,
                    ActorUserId = null, // システムによる通知
                    Payload = System.Text.Json.JsonSerializer.Serialize(new { dueDate = target.DueDate }),
                });
                created++;
            }
        }

        if (created > 0)
        {
            await _dbContext.SaveChangesAsync();
        }

        return created;
    }
}
