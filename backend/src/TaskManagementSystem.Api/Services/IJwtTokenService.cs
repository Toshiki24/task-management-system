using TaskManagementSystem.Api.Models;

namespace TaskManagementSystem.Api.Services;

public interface IJwtTokenService
{
    string GenerateToken(User user);
}
