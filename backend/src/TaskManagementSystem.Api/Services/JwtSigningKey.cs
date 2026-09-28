using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace TaskManagementSystem.Api.Services;

/// <summary>
/// JWTの署名鍵を設定(Jwt:Key)から作成する。署名(発行)と検証の両方で同じ処理を使う。
/// </summary>
public static class JwtSigningKey
{
    /// <summary>HMAC-SHA256 の鍵として必要な長さ(256ビット)。短い鍵は総当たりで推測されるおそれがある</summary>
    public const int MinimumBytes = 32;

    /// <exception cref="InvalidOperationException">鍵が設定されていない、または短すぎる場合</exception>
    public static SymmetricSecurityKey Create(IConfiguration configuration)
    {
        var key = configuration["Jwt:Key"];
        if (string.IsNullOrEmpty(key))
        {
            throw new InvalidOperationException("Jwt:Key が設定されていません。");
        }

        var bytes = Encoding.UTF8.GetBytes(key);
        if (bytes.Length < MinimumBytes)
        {
            throw new InvalidOperationException(
                $"Jwt:Key が短すぎます。{MinimumBytes}バイト以上のランダムな文字列を設定してください(現在 {bytes.Length}バイト)。");
        }

        return new SymmetricSecurityKey(bytes);
    }
}
