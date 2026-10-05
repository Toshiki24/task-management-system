namespace TaskManagementSystem.Api.Dtos.Auth;

/// <summary>ログインAPI・リフレッシュAPIのレスポンス</summary>
/// <param name="AccessTokenExpiresAt">アクセストークンの有効期限(UTC)。BFFが再発行のタイミングを判断するために使う</param>
public record LoginResponse(string AccessToken, DateTime AccessTokenExpiresAt, string RefreshToken, AuthUserDto User);
