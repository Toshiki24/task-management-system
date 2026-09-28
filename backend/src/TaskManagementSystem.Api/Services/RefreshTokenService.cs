using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using TaskManagementSystem.Api.Data;
using TaskManagementSystem.Api.Models;

namespace TaskManagementSystem.Api.Services;

/// <summary>
/// リフレッシュトークンの発行・置き換え(ローテーション)・失効を行う(security-review.md 5.3)。
/// </summary>
/// <remarks>
/// <para>
/// トークンは推測できない乱数(32バイト)とし、DBには SHA-256 のハッシュ値のみを保存する。
/// DBの内容が漏洩しても、そこからトークンを復元して使うことはできない。
/// </para>
/// <para>
/// 置き換え済みのトークンが再び使われた場合は、トークンが盗まれて攻撃者と本人の両方が使っているとみなし、
/// そのユーザーの有効なトークンをすべて失効させる(再利用検知)。
/// ただし、画面から同時に複数のリクエストが送られ、同じトークンでの置き換えがほぼ同時に起きることがあるため、
/// 置き換えから猶予期間(ReuseGraceSeconds)以内の再利用は正常な利用として扱い、新しいトークンを発行する。
/// </para>
/// </remarks>
public class RefreshTokenService : IRefreshTokenService
{
    private readonly AppDbContext _dbContext;
    private readonly TimeProvider _timeProvider;
    private readonly TimeSpan _lifetime;
    private readonly TimeSpan _reuseGracePeriod;

    public RefreshTokenService(AppDbContext dbContext, TimeProvider timeProvider, IConfiguration configuration)
    {
        _dbContext = dbContext;
        _timeProvider = timeProvider;
        var section = configuration.GetSection("RefreshToken");
        _lifetime = TimeSpan.FromDays(section.GetValue("ExpiresDays", 7d));
        _reuseGracePeriod = TimeSpan.FromSeconds(section.GetValue("ReuseGraceSeconds", 10d));
    }

    public async Task<string> IssueAsync(long userId)
    {
        var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');

        _dbContext.RefreshTokens.Add(new RefreshToken
        {
            UserId = userId,
            TokenHash = Hash(token),
            ExpiresAt = Now() + _lifetime,
        });
        await _dbContext.SaveChangesAsync();

        return token;
    }

    public async Task<RotatedRefreshToken?> RotateAsync(string refreshToken)
    {
        var tokenHash = Hash(refreshToken);
        var stored = await _dbContext.RefreshTokens
            .Include(t => t.User)
            .SingleOrDefaultAsync(t => t.TokenHash == tokenHash);
        var now = Now();

        if (stored is null || stored.ExpiresAt <= now)
        {
            return null;
        }

        if (stored.RevokedAt is not null)
        {
            var withinGracePeriod = stored.RevokedReason == RefreshTokenRevokedReason.Rotated
                && now < stored.RevokedAt + _reuseGracePeriod;
            if (!withinGracePeriod)
            {
                if (stored.RevokedReason == RefreshTokenRevokedReason.Rotated)
                {
                    await RevokeAllAsync(stored.UserId, RefreshTokenRevokedReason.ReuseDetected);
                }

                return null;
            }

            // 猶予期間内の再利用(同時リクエスト)は、失効日時を変えずに新しいトークンだけを発行する
        }
        else
        {
            stored.RevokedAt = now;
            stored.RevokedReason = RefreshTokenRevokedReason.Rotated;
        }

        var newToken = await IssueAsync(stored.UserId);
        return new RotatedRefreshToken(stored.User, newToken);
    }

    public async Task RevokeAsync(string refreshToken)
    {
        var tokenHash = Hash(refreshToken);
        var stored = await _dbContext.RefreshTokens
            .SingleOrDefaultAsync(t => t.TokenHash == tokenHash);
        if (stored is null || stored.RevokedAt is not null)
        {
            return;
        }

        stored.RevokedAt = Now();
        stored.RevokedReason = RefreshTokenRevokedReason.Logout;
        await _dbContext.SaveChangesAsync();
    }

    private async Task RevokeAllAsync(long userId, string reason)
    {
        var active = await _dbContext.RefreshTokens
            .Where(t => t.UserId == userId && t.RevokedAt == null)
            .ToListAsync();
        var now = Now();
        foreach (var token in active)
        {
            token.RevokedAt = now;
            token.RevokedReason = reason;
        }

        await _dbContext.SaveChangesAsync();
    }

    // 列は timestamp without time zone のため、値はUTCのまま Kind を Unspecified にして扱う(AppDbContext と同じ方針)
    private DateTime Now() => DateTime.SpecifyKind(_timeProvider.GetUtcNow().UtcDateTime, DateTimeKind.Unspecified);

    private static string Hash(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token))).ToLowerInvariant();
}
