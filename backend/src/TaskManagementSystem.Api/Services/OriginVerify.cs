using System.Security.Cryptography;
using System.Text;

namespace TaskManagementSystem.Api.Services;

/// <summary>
/// BFF からの呼び出しであることを確認する共有シークレット(<c>X-Origin-Verify</c>)の、
/// 設定の読み込み・強度チェックと、処理時間が値に依存しない照合を行う。
/// 公開されている API Gateway のURLを BFF を経由せずに直接呼び出す攻撃を防ぐ(security-review-2.md SEC2-01 / SEC2-02、aws-architecture.md 5.3)。
/// </summary>
public static class OriginVerify
{
    /// <summary>BFF が付与し、API が検証するヘッダー名。</summary>
    public const string HeaderName = "X-Origin-Verify";

    /// <summary>共有シークレットに必要な最小の長さ(バイト)。推測されないよう十分に長くする。</summary>
    public const int MinimumBytes = 32;

    /// <summary>
    /// 設定(<c>OriginVerify:Secret</c>)から共有シークレットを読み込む。
    /// 本番では未設定・短すぎる場合に起動時エラーにする(フェイルクローズ。security-review-2.md SEC2-02)。
    /// 本番以外で未設定の場合は <c>null</c> を返し、検証を行わない(ローカル開発を妨げない)。
    /// </summary>
    /// <exception cref="InvalidOperationException">本番で未設定、または短すぎる場合</exception>
    public static string? ResolveSecret(IConfiguration configuration, bool isProduction)
    {
        var secret = configuration["OriginVerify:Secret"];

        if (string.IsNullOrEmpty(secret))
        {
            if (isProduction)
            {
                throw new InvalidOperationException("OriginVerify:Secret が設定されていません。");
            }

            return null;
        }

        if (isProduction && Encoding.UTF8.GetByteCount(secret) < MinimumBytes)
        {
            throw new InvalidOperationException(
                $"OriginVerify:Secret が短すぎます。{MinimumBytes}バイト以上のランダムな文字列を設定してください。");
        }

        return secret;
    }

    /// <summary>
    /// 受け取ったヘッダー値が共有シークレットと一致するかを、処理時間が値に依存しない方法で判定する。
    /// 値の長さの違いが処理時間に表れないよう、双方を同じ長さのハッシュにしてから比較する。
    /// </summary>
    public static bool IsValid(string? provided, string expected)
    {
        if (provided is null)
        {
            return false;
        }

        Span<byte> providedHash = stackalloc byte[SHA256.HashSizeInBytes];
        Span<byte> expectedHash = stackalloc byte[SHA256.HashSizeInBytes];
        SHA256.HashData(Encoding.UTF8.GetBytes(provided), providedHash);
        SHA256.HashData(Encoding.UTF8.GetBytes(expected), expectedHash);

        return CryptographicOperations.FixedTimeEquals(providedHash, expectedHash);
    }
}
