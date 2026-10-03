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
    /// <param name="clientIp">BFFが転送した実クライアントIP。IP単位のログイン制限に使う。null の場合はIP単位で数えない。</param>
    Task<LoginOutcome> LoginAsync(LoginRequest request, string? clientIp = null);

    /// <summary>リフレッシュトークンでアクセストークンを再発行する。無効なトークンの場合は null を返す。</summary>
    Task<LoginResponse?> RefreshAsync(RefreshTokenRequest request);

    Task LogoutAsync(RefreshTokenRequest request);
}
