using Microsoft.EntityFrameworkCore;
using TaskManagementSystem.Api.Data;
using TaskManagementSystem.Api.Dtos.Common;

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

    private static UserDto ToDto(Models.User user) => new(user.Id, user.Name, user.Email);
}
