using TaskManagementSystem.Api.Dtos.Common;
using TaskManagementSystem.Api.Dtos.Users;

namespace TaskManagementSystem.Api.Services;

public interface IUserService
{
    Task<List<UserDto>> GetAllAsync();
    Task<UserDto?> GetByIdAsync(long id);

    /// <summary>本人のメール通知設定を取得する(ユーザーが存在しなければ null)。</summary>
    Task<NotificationSettingsDto?> GetNotificationSettingsAsync(long userId);

    /// <summary>本人のメール通知設定を更新する(更新できたら true、ユーザー不在なら false)。</summary>
    Task<bool> UpdateNotificationSettingsAsync(long userId, bool emailEnabled);
}
