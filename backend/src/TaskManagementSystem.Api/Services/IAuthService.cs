using TaskManagementSystem.Api.Dtos.Auth;

namespace TaskManagementSystem.Api.Services;

public enum LoginResult
{
    Success,
    InvalidCredentials,
    TooManyAttempts,
}

public record LoginOutcome(LoginResult Result, LoginResponse? Data = null, TimeSpan? RetryAfter = null);

public interface IAuthService
{
    Task<LoginOutcome> LoginAsync(LoginRequest request);
}
