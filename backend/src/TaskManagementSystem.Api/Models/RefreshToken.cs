namespace TaskManagementSystem.Api.Models;

public static class RefreshTokenRevokedReason
{
    /// <summary>リフレッシュ時に新しいトークンへ置き換えられた</summary>
    public const string Rotated = "ROTATED";

    /// <summary>ログアウトにより失効した</summary>
    public const string Logout = "LOGOUT";

    /// <summary>置き換え済みのトークンが再利用されたため、漏洩とみなして失効させた</summary>
    public const string ReuseDetected = "REUSE_DETECTED";
}

/// <summary>
/// DBの "refresh_tokens" テーブルに対応するEntity。
/// トークンそのものは保存せず、SHA-256のハッシュ値のみを保存する(security-review.md 5.3)。
/// </summary>
public class RefreshToken
{
    public long Id { get; set; }
    public long UserId { get; set; }
    public string TokenHash { get; set; } = null!;
    public DateTime ExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? RevokedAt { get; set; }
    public string? RevokedReason { get; set; }

    public User User { get; set; } = null!;
}
