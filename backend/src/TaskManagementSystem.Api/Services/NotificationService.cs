using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TaskManagementSystem.Api.Data;
using TaskManagementSystem.Api.Dtos.Common;
using TaskManagementSystem.Api.Dtos.Notifications;

namespace TaskManagementSystem.Api.Services;

/// <summary>
/// アプリ内通知の参照・既読化(M3 §6)。通知の生成は各サービスが NotificationRecorder 経由で行う。
/// 取得は本人宛のみ。すべて LINQ/EF。
/// </summary>
public class NotificationService : INotificationService
{
    private readonly AppDbContext _dbContext;

    public NotificationService(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<PagedResult<NotificationDto>> GetAsync(long currentUserId, NotificationQuery query)
    {
        var page = query.Page is > 0 ? query.Page.Value : 1;
        var pageSize = query.PageSize is > 0 and <= 100 ? query.PageSize.Value : 20;

        var q = _dbContext.Notifications.Where(n => n.RecipientUserId == currentUserId);
        if (query.UnreadOnly == true)
        {
            q = q.Where(n => !n.IsRead);
        }

        var total = await q.CountAsync();

        var rows = await q
            .OrderByDescending(n => n.CreatedAt)
            .ThenByDescending(n => n.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(n => new
            {
                n.Id,
                n.Type,
                n.TaskId,
                TaskTitle = n.Task != null ? n.Task.Title : null,
                n.ActorUserId,
                ActorName = n.ActorUser != null ? n.ActorUser.Name : null,
                n.Payload,
                n.IsRead,
                n.CreatedAt,
            })
            .ToListAsync();

        var items = rows
            .Select(r => new NotificationDto(
                r.Id, r.Type, r.TaskId, r.TaskTitle, r.ActorUserId, r.ActorName,
                ParsePayload(r.Payload), r.IsRead, r.CreatedAt))
            .ToList();

        return new PagedResult<NotificationDto>(items, page, pageSize, total);
    }

    public Task<int> GetUnreadCountAsync(long currentUserId) =>
        _dbContext.Notifications.CountAsync(n => n.RecipientUserId == currentUserId && !n.IsRead);

    public async Task<bool> MarkReadAsync(long id, long currentUserId)
    {
        var notification = await _dbContext.Notifications
            .FirstOrDefaultAsync(n => n.Id == id && n.RecipientUserId == currentUserId);
        if (notification is null)
        {
            return false;
        }

        if (!notification.IsRead)
        {
            notification.IsRead = true;
            notification.ReadAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);
            await _dbContext.SaveChangesAsync();
        }

        return true;
    }

    public async Task<int> MarkAllReadAsync(long currentUserId)
    {
        var unread = await _dbContext.Notifications
            .Where(n => n.RecipientUserId == currentUserId && !n.IsRead)
            .ToListAsync();

        var now = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);
        foreach (var notification in unread)
        {
            notification.IsRead = true;
            notification.ReadAt = now;
        }

        if (unread.Count > 0)
        {
            await _dbContext.SaveChangesAsync();
        }

        return unread.Count;
    }

    private static JsonElement? ParsePayload(string? payload)
    {
        if (string.IsNullOrWhiteSpace(payload))
        {
            return null;
        }

        try
        {
            return JsonDocument.Parse(payload).RootElement.Clone();
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
