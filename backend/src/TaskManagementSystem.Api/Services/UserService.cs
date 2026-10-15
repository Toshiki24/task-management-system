using Microsoft.EntityFrameworkCore;
using TaskManagementSystem.Api.Data;
using TaskManagementSystem.Api.Dtos.Common;
using TaskManagementSystem.Api.Dtos.Users;

namespace TaskManagementSystem.Api.Services;

public class UserService : IUserService
{
    private readonly AppDbContext _dbContext;

    public UserService(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<List<UserDto>> GetAllAsync()
    {
        var users = await _dbContext.Users
            .OrderBy(u => u.Id)
            .ToListAsync();

        return users.Select(ToDto).ToList();
    }

    public async Task<UserDto?> GetByIdAsync(long id)
    {
        var user = await _dbContext.Users.FindAsync(id);
        return user is null ? null : ToDto(user);
    }

    public async Task<NotificationSettingsDto?> GetNotificationSettingsAsync(long userId)
    {
        var user = await _dbContext.Users
            .Where(u => u.Id == userId)
            .Select(u => new { u.EmailNotificationsEnabled })
            .FirstOrDefaultAsync();
        return user is null ? null : new NotificationSettingsDto(user.EmailNotificationsEnabled);
    }

    public async Task<bool> UpdateNotificationSettingsAsync(long userId, bool emailEnabled)
    {
        var updated = await _dbContext.Users
            .Where(u => u.Id == userId)
            .ExecuteUpdateAsync(s => s.SetProperty(u => u.EmailNotificationsEnabled, emailEnabled));
        return updated > 0;
    }

    private static UserDto ToDto(Models.User user) => new(user.Id, user.Name, user.Email);
}
