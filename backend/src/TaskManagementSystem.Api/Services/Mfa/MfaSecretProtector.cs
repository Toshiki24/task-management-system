using System.Security.Cryptography;
using System.Text;

namespace TaskManagementSystem.Api.Services.Mfa;

public interface IMfaSecretProtector
{
    /// <summary>TOTP シークレット(Base32 文字列)を暗号化して保存用の文字列にする。</summary>
    string Protect(string plaintext);

    /// <summary>保存された暗号文を復号して TOTP シークレットに戻す。</summary>
    string Unprotect(string ciphertext);
}

/// <summary>
/// TOTP シークレットを AES-256-GCM で暗号化して保存する(M5 §6: DB 平文に置かない)。
/// 鍵は設定 <c>Mfa:Key</c>(無ければ <c>Jwt:Key</c>)から SHA-256 で導出する。
/// 形式: base64(nonce(12) + tag(16) + ciphertext)。
/// </summary>
public class MfaSecretProtector : IMfaSecretProtector
{
    private const int NonceSize = 12;
    private const int TagSize = 16;

    private readonly byte[] _key;

    public MfaSecretProtector(IConfiguration configuration)
    {
        var secret = configuration["Mfa:Key"] ?? configuration["Jwt:Key"];
        if (string.IsNullOrEmpty(secret))
        {
            throw new InvalidOperationException("Mfa:Key または Jwt:Key が設定されていません。");
        }

        // 任意長の設定値から 256bit 鍵を導出する
        _key = SHA256.HashData(Encoding.UTF8.GetBytes(secret));
    }

    public string Protect(string plaintext)
    {
        var plainBytes = Encoding.UTF8.GetBytes(plaintext);
        var nonce = RandomNumberGenerator.GetBytes(NonceSize);
        var cipherBytes = new byte[plainBytes.Length];
        var tag = new byte[TagSize];

        using var aes = new AesGcm(_key, TagSize);
        aes.Encrypt(nonce, plainBytes, cipherBytes, tag);

        var combined = new byte[NonceSize + TagSize + cipherBytes.Length];
        Buffer.BlockCopy(nonce, 0, combined, 0, NonceSize);
        Buffer.BlockCopy(tag, 0, combined, NonceSize, TagSize);
        Buffer.BlockCopy(cipherBytes, 0, combined, NonceSize + TagSize, cipherBytes.Length);
        return Convert.ToBase64String(combined);
    }

    public string Unprotect(string ciphertext)
    {
        var combined = Convert.FromBase64String(ciphertext);
        if (combined.Length < NonceSize + TagSize)
        {
            throw new FormatException("MFA シークレットの暗号文が不正です。");
        }

        var nonce = combined.AsSpan(0, NonceSize);
        var tag = combined.AsSpan(NonceSize, TagSize);
        var cipherBytes = combined.AsSpan(NonceSize + TagSize);
        var plainBytes = new byte[cipherBytes.Length];

        using var aes = new AesGcm(_key, TagSize);
        aes.Decrypt(nonce, cipherBytes, tag, plainBytes);
        return Encoding.UTF8.GetString(plainBytes);
    }
}
