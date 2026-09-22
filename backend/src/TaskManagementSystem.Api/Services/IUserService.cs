using TaskManagementSystem.Api.Dtos.Common;

namespace TaskManagementSystem.Api.Services;

public interface IUserService
{
    Task<List<UserDto>> GetAllAsync();
    Task<UserDto?> GetByIdAsync(long id);
}
