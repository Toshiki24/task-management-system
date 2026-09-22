using TaskManagementSystem.Api.Dtos.Auth;

namespace TaskManagementSystem.Api.Services;

public interface IAuthService
{
    Task<LoginResponse?> LoginAsync(LoginRequest request);
}
