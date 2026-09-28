namespace TaskManagementSystem.Api.Services;

/// <summary>
/// メールアドレス(アカウント)単位で、ログイン失敗回数を制限する(security-review.md 5.2)。
/// </summary>
public interface ILoginAttemptLimiter
{
    /// <summary>ログインが制限されている場合は解除までの残り時間を、制限されていない場合は null を返す。</summary>
    TimeSpan? GetRetryAfter(string email);

    void RecordFailure(string email);

    void Reset(string email);
}
