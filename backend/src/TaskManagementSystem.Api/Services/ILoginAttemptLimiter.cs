namespace TaskManagementSystem.Api.Services;

/// <summary>
/// ログイン失敗回数を制限する。メールアドレス(アカウント)単位に加え、
/// 送信元IPアドレス単位でも数え、多数のアカウントに同じパスワードを試す攻撃(パスワードスプレー)を抑える
/// (security-review.md 5.2、security-review-2.md SEC2-03)。
/// </summary>
public interface ILoginAttemptLimiter
{
    /// <summary>
    /// ログインが制限されている場合は解除までの残り時間を、制限されていない場合は null を返す。
    /// メールアドレス単位・IP単位のいずれかが上限に達していれば制限とみなし、残り時間の長い方を返す。
    /// </summary>
    /// <param name="ipAddress">BFFが転送した実クライアントIP。null の場合はIP単位の判定を行わない。</param>
    TimeSpan? GetRetryAfter(string email, string? ipAddress = null);

    /// <summary>ログイン失敗を記録する。IPが指定された場合はIP単位の失敗も記録する。</summary>
    void RecordFailure(string email, string? ipAddress = null);

    /// <summary>ログイン成功時に、そのアカウントの失敗回数をリセットする(IP単位の記録は期間満了で解除される)。</summary>
    void Reset(string email);
}
