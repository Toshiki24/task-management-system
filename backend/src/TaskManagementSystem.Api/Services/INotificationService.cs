using TaskManagementSystem.Api.Dtos.Common;
using TaskManagementSystem.Api.Dtos.Notifications;

namespace TaskManagementSystem.Api.Services;

public interface INotificationService
{
    Task<PagedResult<NotificationDto>> GetAsync(long currentUserId, NotificationQuery query);
    Task<int> GetUnreadCountAsync(long currentUserId);

    /// <summary>自分宛の 1 件を既読にする。該当が無ければ false。</summary>
    Task<bool> MarkReadAsync(long id, long currentUserId);

    /// <summary>自分宛の未読をすべて既読にする。既読にした件数を返す。</summary>
    Task<int> MarkAllReadAsync(long currentUserId);
}
