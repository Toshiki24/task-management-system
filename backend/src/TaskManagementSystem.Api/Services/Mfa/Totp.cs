using System.Security.Cryptography;

namespace TaskManagementSystem.Api.Services.Mfa;

/// <summary>
/// RFC 6238 の TOTP(時間ベースのワンタイムパスワード)を提供する(M5 §6)。
/// 認証アプリ(Google Authenticator 等)と同じ HMAC-SHA1・30秒ステップ・6桁の既定で実装する。
/// </summary>
public static class Totp
{
    private const int DigitsDefault = 6;
    private const int StepSecondsDefault = 30;

    /// <summary>新しい TOTP シークレット(Base32 文字列)を生成する。既定で 160bit(20バイト)。</summary>
    public static string GenerateSecret(int bytes = 20)
    {
        return Base32.Encode(RandomNumberGenerator.GetBytes(bytes));
    }

    /// <summary>
    /// 指定時刻におけるコードを計算する。<paramref name="secret"/> は Base32 文字列。
    /// </summary>
    public static string Compute(string secret, DateTimeOffset timestamp, int digits = DigitsDefault, int stepSeconds = StepSecondsDefault)
    {
        var counter = timestamp.ToUnixTimeSeconds() / stepSeconds;
        return ComputeForCounter(Base32.Decode(secret), counter, digits);
    }

    /// <summary>
    /// コードを検証する。時刻ずれを許容するため、前後 <paramref name="window"/> ステップまで一致を許す。
    /// </summary>
    public static bool Verify(string secret, string code, DateTimeOffset now, int window = 1, int digits = DigitsDefault, int stepSeconds = StepSecondsDefault)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return false;
        }

        code = code.Trim();
        byte[] key;
        try
        {
            key = Base32.Decode(secret);
        }
        catch (FormatException)
        {
            return false;
        }

        var baseCounter = now.ToUnixTimeSeconds() / stepSeconds;
        for (var offset = -window; offset <= window; offset++)
        {
            var candidate = ComputeForCounter(key, baseCounter + offset, digits);
            // タイミング攻撃を避けるため固定時間比較する
            if (CryptographicOperations.FixedTimeEquals(
                System.Text.Encoding.ASCII.GetBytes(candidate),
                System.Text.Encoding.ASCII.GetBytes(code)))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>認証アプリ登録用の otpauth URI を組み立てる。</summary>
    public static string BuildUri(string secret, string issuer, string account, int digits = DigitsDefault, int stepSeconds = StepSecondsDefault)
    {
        var label = Uri.EscapeDataString($"{issuer}:{account}");
        var issuerParam = Uri.EscapeDataString(issuer);
        return $"otpauth://totp/{label}?secret={secret}&issuer={issuerParam}"
            + $"&algorithm=SHA1&digits={digits}&period={stepSeconds}";
    }

    private static string ComputeForCounter(byte[] key, long counter, int digits)
    {
        var counterBytes = new byte[8];
        for (var i = 7; i >= 0; i--)
        {
            counterBytes[i] = (byte)(counter & 0xff);
            counter >>= 8;
        }

        var hash = HMACSHA1.HashData(key, counterBytes);

        // RFC 4226 のダイナミックトランケーション
        var offset = hash[^1] & 0x0f;
        var binary = ((hash[offset] & 0x7f) << 24)
            | ((hash[offset + 1] & 0xff) << 16)
            | ((hash[offset + 2] & 0xff) << 8)
            | (hash[offset + 3] & 0xff);

        var otp = binary % (int)Math.Pow(10, digits);
        return otp.ToString().PadLeft(digits, '0');
    }
}
