using System.Security.Cryptography;
using System.Text;

namespace TaskManagementSystem.Api.Services.Mfa;

public interface IMfaChallengeTokenService
{
    /// <summary>一次認証(パスワード)成功後に発行する短命トークン。MFA 検証ステップでのみ使う。</summary>
    string Issue(long userId);

    /// <summary>トークンを検証し、対象ユーザー ID を返す。無効・期限切れなら null。</summary>
    long? Validate(string token);
}

/// <summary>
/// MFA チャレンジトークン(M5 §6)。パスワード認証後〜TOTP 検証前のあいだだけ有効な短命トークン。
/// セッション(リフレッシュトークン)はまだ発行しないため、独自の HMAC 署名トークンで橋渡しする。
/// 形式: base64url(userId:expiryUnix) + "." + base64url(HMAC-SHA256)。
/// </summary>
public class MfaChallengeTokenService : IMfaChallengeTokenService
{
    public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(5);

    private readonly byte[] _key;
    private readonly TimeProvider _timeProvider;

    public MfaChallengeTokenService(IConfiguration configuration, TimeProvider timeProvider)
    {
        var secret = configuration["Mfa:Key"] ?? configuration["Jwt:Key"];
        if (string.IsNullOrEmpty(secret))
        {
            throw new InvalidOperationException("Mfa:Key または Jwt:Key が設定されていません。");
        }

        // JWT 署名鍵とは用途を分けるため、ラベル付きで別の鍵を導出する
        _key = SHA256.HashData(Encoding.UTF8.GetBytes("mfa-challenge:" + secret));
        _timeProvider = timeProvider;
    }

    public string Issue(long userId)
    {
        var expiry = _timeProvider.GetUtcNow().Add(Lifetime).ToUnixTimeSeconds();
        var payload = $"{userId}:{expiry}";
        var payloadBytes = Encoding.UTF8.GetBytes(payload);
        var signature = HMACSHA256.HashData(_key, payloadBytes);
        return $"{Base64Url(payloadBytes)}.{Base64Url(signature)}";
    }

    public long? Validate(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        var parts = token.Split('.');
        if (parts.Length != 2)
        {
            return null;
        }

        byte[] payloadBytes;
        byte[] signature;
        try
        {
            payloadBytes = FromBase64Url(parts[0]);
            signature = FromBase64Url(parts[1]);
        }
        catch (FormatException)
        {
            return null;
        }

        var expected = HMACSHA256.HashData(_key, payloadBytes);
        if (!CryptographicOperations.FixedTimeEquals(expected, signature))
        {
            return null;
        }

        var payload = Encoding.UTF8.GetString(payloadBytes);
        var fields = payload.Split(':');
        if (fields.Length != 2
            || !long.TryParse(fields[0], out var userId)
            || !long.TryParse(fields[1], out var expiryUnix))
        {
            return null;
        }

        if (_timeProvider.GetUtcNow().ToUnixTimeSeconds() > expiryUnix)
        {
            return null;
        }

        return userId;
    }

    private static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static byte[] FromBase64Url(string value)
    {
        var padded = value.Replace('-', '+').Replace('_', '/');
        padded = (padded.Length % 4) switch
        {
            2 => padded + "==",
            3 => padded + "=",
            0 => padded,
            _ => throw new FormatException("Base64url として不正な長さです。"),
        };
        return Convert.FromBase64String(padded);
    }
}
