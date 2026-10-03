using Microsoft.Extensions.Caching.Memory;

namespace TaskManagementSystem.Api.Services;

/// <summary>
/// 最初の失敗から一定期間(FailureWindowSeconds)内の失敗回数が上限に達したら、その期間が終わるまでログインを制限する。
/// メールアドレス(アカウント)単位(MaxFailedAttempts)と、送信元IP単位(MaxFailedAttemptsPerIp)の両方で数える。
/// </summary>
/// <remarks>
/// <para>
/// メールアドレス単位は同じアカウントへの総当たりを防ぐ。IP単位は、多数のアカウントに同じパスワードを試す攻撃
/// (パスワードスプレー)を防ぐ(security-review-2.md SEC2-03)。IPは BFF が転送する実クライアントIPを用いる
/// (API は X-Origin-Verify で BFF からの呼び出しのみを受け付けるため、転送されたIPを信頼できる)。
/// </para>
/// <para>
/// 失敗回数はメモリ上に保持するため、アプリの再起動で消え、複数インスタンス間では共有されない(security-review-2.md §5.4 で受容)。
/// 登録されていないメールアドレスも同じように数え、制限の有無から登録状況を推測できないようにする。
/// </para>
/// </remarks>
public sealed class LoginAttemptLimiter : ILoginAttemptLimiter, IDisposable
{
    // 大量の異なるキー(メール・IP)で試行されてもメモリを使い切らないよう、保持件数に上限を設ける。
    private const long MaxTrackedKeys = 100_000;

    private readonly MemoryCache _failures = new(new MemoryCacheOptions { SizeLimit = MaxTrackedKeys });
    private readonly object _lock = new();
    private readonly TimeProvider _timeProvider;
    private readonly int _maxFailedAttempts;
    private readonly int _maxFailedAttemptsPerIp;
    private readonly TimeSpan _failureWindow;

    public LoginAttemptLimiter(IConfiguration configuration, TimeProvider timeProvider)
    {
        var section = configuration.GetSection("LoginProtection");
        _maxFailedAttempts = section.GetValue("MaxFailedAttempts", 5);
        // IPは複数の利用者を共有しうる(社内NAT等)ため、メール単位より緩めの上限にする
        _maxFailedAttemptsPerIp = section.GetValue("MaxFailedAttemptsPerIp", 20);
        _failureWindow = TimeSpan.FromSeconds(section.GetValue("FailureWindowSeconds", 60));
        _timeProvider = timeProvider;
    }

    public TimeSpan? GetRetryAfter(string email, string? ipAddress = null)
    {
        lock (_lock)
        {
            var byEmail = GetRetryAfterForKey(EmailKey(email), _maxFailedAttempts);
            var byIp = string.IsNullOrWhiteSpace(ipAddress)
                ? null
                : GetRetryAfterForKey(IpKey(ipAddress), _maxFailedAttemptsPerIp);

            // メール単位・IP単位のいずれかが制限中なら、残り時間の長い方を返す
            return Longer(byEmail, byIp);
        }
    }

    public void RecordFailure(string email, string? ipAddress = null)
    {
        lock (_lock)
        {
            RecordFailureForKey(EmailKey(email));
            if (!string.IsNullOrWhiteSpace(ipAddress))
            {
                RecordFailureForKey(IpKey(ipAddress));
            }
        }
    }

    public void Reset(string email)
    {
        lock (_lock)
        {
            // 成功したアカウントの失敗回数のみ解除する。IP単位の記録は期間満了で解除する
            // (1回の成功でIP単位の記録を消せると、正しい資格情報を1つ持つ攻撃者がスプレーの制限を回避できるため)
            _failures.Remove(EmailKey(email));
        }
    }

    public void Dispose() => _failures.Dispose();

    private TimeSpan? GetRetryAfterForKey(string key, int maxAttempts)
    {
        var entry = GetActiveEntry(key);
        if (entry is null || entry.Count < maxAttempts)
        {
            return null;
        }

        return entry.WindowStart + _failureWindow - _timeProvider.GetUtcNow();
    }

    private void RecordFailureForKey(string key)
    {
        var entry = GetActiveEntry(key);
        if (entry is null)
        {
            entry = new FailureEntry(_timeProvider.GetUtcNow());
            // 期間が過ぎた記録はGetActiveEntryで無視されるが、メモリから消えるよう有効期限も設定する
            _failures.Set(key, entry, new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = _failureWindow,
                Size = 1,
            });
        }

        entry.Count++;
    }

    private FailureEntry? GetActiveEntry(string key)
    {
        if (!_failures.TryGetValue(key, out FailureEntry? entry) || entry is null)
        {
            return null;
        }

        if (_timeProvider.GetUtcNow() >= entry.WindowStart + _failureWindow)
        {
            _failures.Remove(key);
            return null;
        }

        return entry;
    }

    private static TimeSpan? Longer(TimeSpan? a, TimeSpan? b)
    {
        if (a is null)
        {
            return b;
        }

        if (b is null)
        {
            return a;
        }

        return a.Value >= b.Value ? a : b;
    }

    // 大文字・小文字や前後の空白を変えるだけで制限を回避できないよう、正規化してから数える。
    // メールとIPでキー空間を分けるため接頭辞を付ける
    private static string EmailKey(string email) => "e:" + email.Trim().ToLowerInvariant();

    private static string IpKey(string ipAddress) => "i:" + ipAddress.Trim();

    private sealed class FailureEntry(DateTimeOffset windowStart)
    {
        public DateTimeOffset WindowStart { get; } = windowStart;
        public int Count { get; set; }
    }
}
