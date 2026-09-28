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

    /// <summary>リフレッシュトークンでアクセストークンを再発行する。無効なトークンの場合は null を返す。</summary>
    Task<LoginResponse?> RefreshAsync(RefreshTokenRequest request);

    Task LogoutAsync(RefreshTokenRequest request);
}
