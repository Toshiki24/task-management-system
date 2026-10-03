using System.Security.Cryptography;
using System.Text;

namespace TaskManagementSystem.Api.Services;

/// <summary>
/// BFF からの呼び出しであることを確認する共有シークレット(<c>X-Origin-Verify</c>)の、
/// 設定の読み込み・強度チェックと、処理時間が値に依存しない照合を行う。
/// 公開されている API Gateway のURLを BFF を経由せずに直接呼び出す攻撃を防ぐ(security-review-2.md SEC2-01 / SEC2-02、aws-architecture.md 5.3)。
/// </summary>
/// <remarks>
/// 無停止でシークレットを入れ替えられるよう、現行(<c>OriginVerify:Secret</c>)に加えて、ローテーション中は
/// 旧値(<c>OriginVerify:PreviousSecret</c>)も受け付ける。入れ替え手順は以下のとおり(security-review-2.md SEC2-02)。
/// <list type="number">
/// <item>APIに PreviousSecret=旧値 / Secret=新値 を設定してデプロイ(新旧どちらも受け付ける)。</item>
/// <item>BFFの共有シークレットを新値に更新してデプロイ(以後は新値を送る)。</item>
/// <item>APIの PreviousSecret を削除してデプロイ(新値のみ受け付ける)。</item>
/// </list>
/// </remarks>
public static class OriginVerify
{
    /// <summary>BFF が付与し、API が検証するヘッダー名。</summary>
    public const string HeaderName = "X-Origin-Verify";

    /// <summary>共有シークレットに必要な最小の長さ(バイト)。推測されないよう十分に長くする。</summary>
    public const int MinimumBytes = 32;

    private const string SecretKey = "OriginVerify:Secret";
    private const string PreviousSecretKey = "OriginVerify:PreviousSecret";

    /// <summary>
    /// 受け付ける共有シークレットの一覧を設定から読み込む。現行(<c>OriginVerify:Secret</c>)に加え、
    /// ローテーション中は旧値(<c>OriginVerify:PreviousSecret</c>)も含める。
    /// 本番では現行が未設定・短すぎる場合に起動時エラーにする(フェイルクローズ)。
    /// 本番以外で現行が未設定の場合は空の一覧を返し、検証を行わない(ローカル開発を妨げない)。
    /// </summary>
    /// <exception cref="InvalidOperationException">本番で現行が未設定、またはいずれかの値が短すぎる場合</exception>
    public static IReadOnlyList<string> ResolveSecrets(IConfiguration configuration, bool isProduction)
    {
        var secrets = new List<string>();
        var current = configuration[SecretKey];

        if (string.IsNullOrEmpty(current))
        {
            if (isProduction)
            {
                throw new InvalidOperationException("OriginVerify:Secret が設定されていません。");
            }

            return secrets;
        }

        AddValidated(secrets, current, isProduction);

        // ローテーション中のみ設定される旧値。設定されていれば併せて受け付ける
        var previous = configuration[PreviousSecretKey];
        if (!string.IsNullOrEmpty(previous))
        {
            AddValidated(secrets, previous, isProduction);
        }

        return secrets;
    }

    private static void AddValidated(List<string> secrets, string secret, bool isProduction)
    {
        if (isProduction && Encoding.UTF8.GetByteCount(secret) < MinimumBytes)
        {
            throw new InvalidOperationException(
                $"OriginVerify のシークレットが短すぎます。{MinimumBytes}バイト以上のランダムな文字列を設定してください。");
        }

        secrets.Add(secret);
    }

    /// <summary>
    /// 受け取ったヘッダー値が、受け付ける共有シークレットのいずれかと一致するかを、
    /// 処理時間が値に依存しない方法で判定する。
    /// 値の長さの違いが処理時間に表れないよう双方を同じ長さのハッシュにして比較し、
    /// どの値に一致したかを処理時間で区別できないよう、一致後も残りの候補まで必ず比較する。
    /// </summary>
    public static bool IsValid(string? provided, IReadOnlyList<string> acceptedSecrets)
    {
        if (provided is null || acceptedSecrets.Count == 0)
        {
            return false;
        }

        Span<byte> providedHash = stackalloc byte[SHA256.HashSizeInBytes];
        SHA256.HashData(Encoding.UTF8.GetBytes(provided), providedHash);

        Span<byte> expectedHash = stackalloc byte[SHA256.HashSizeInBytes];
        var matched = false;
        foreach (var secret in acceptedSecrets)
        {
            SHA256.HashData(Encoding.UTF8.GetBytes(secret), expectedHash);
            // |= は短絡しないため、一致後も全候補を比較する(どの候補に一致したかを処理時間で区別させない)
            matched |= CryptographicOperations.FixedTimeEquals(providedHash, expectedHash);
        }

        return matched;
    }
}
