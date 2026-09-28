using TaskManagementSystem.Api.Models;

namespace TaskManagementSystem.Api.Services;

/// <param name="User">トークンの持ち主</param>
/// <param name="NewRefreshToken">置き換え後の新しいリフレッシュトークン(平文)</param>
public record RotatedRefreshToken(User User, string NewRefreshToken);

public interface IRefreshTokenService
{
    /// <summary>新しいリフレッシュトークンを発行し、平文を返す(DBにはハッシュ値のみ保存する)。</summary>
    Task<string> IssueAsync(long userId);

    /// <summary>
    /// リフレッシュトークンを新しいトークンに置き換える。無効なトークンの場合は null を返す。
    /// </summary>
    Task<RotatedRefreshToken?> RotateAsync(string refreshToken);

    /// <summary>リフレッシュトークンを失効させる(ログアウト)。存在しない・失効済みの場合は何もしない。</summary>
    Task RevokeAsync(string refreshToken);
}
