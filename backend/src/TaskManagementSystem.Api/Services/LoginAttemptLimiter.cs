using Microsoft.Extensions.Caching.Memory;

namespace TaskManagementSystem.Api.Services;

/// <summary>
/// 最初の失敗から一定期間(FailureWindowSeconds)内の失敗回数が上限(MaxFailedAttempts)に達したら、
/// その期間が終わるまで同じメールアドレスでのログインを制限する。
/// </summary>
/// <remarks>
/// <para>
/// IPアドレスではなくメールアドレス単位で数える。本番ではBFF経由の通信となりAPIから見た送信元IPが共通になるため、
/// IP単位の制限はAPI Gateway / AWS WAF で行う(security-review.md 5.2)。
/// </para>
/// <para>
/// 失敗回数はメモリ上に保持するため、アプリの再起動で消え、複数インスタンス間では共有されない。
/// 登録されていないメールアドレスも同じように数え、制限の有無から登録状況を推測できないようにする。
/// </para>
/// </remarks>
public sealed class LoginAttemptLimiter : ILoginAttemptLimiter, IDisposable
{
    // 大量の異なるメールアドレスで試行されてもメモリを使い切らないよう、保持件数に上限を設ける。
    // 上限に達している間は新しいメールアドレスの失敗を記録できないが、そのような大量の試行は
    // 本番ではAPI Gateway / AWS WAF のIP単位の制限で先に遮断する前提とする。
    private const long MaxTrackedEmails = 100_000;

    private readonly MemoryCache _failures = new(new MemoryCacheOptions { SizeLimit = MaxTrackedEmails });
    private readonly object _lock = new();
    private readonly TimeProvider _timeProvider;
    private readonly int _maxFailedAttempts;
    private readonly TimeSpan _failureWindow;

    public LoginAttemptLimiter(IConfiguration configuration, TimeProvider timeProvider)
    {
        var section = configuration.GetSection("LoginProtection");
        _maxFailedAttempts = section.GetValue("MaxFailedAttempts", 5);
        _failureWindow = TimeSpan.FromSeconds(section.GetValue("FailureWindowSeconds", 60));
        _timeProvider = timeProvider;
    }

    public TimeSpan? GetRetryAfter(string email)
    {
        lock (_lock)
        {
            var entry = GetActiveEntry(Normalize(email));
            if (entry is null || entry.Count < _maxFailedAttempts)
            {
                return null;
            }

            return entry.WindowStart + _failureWindow - _timeProvider.GetUtcNow();
        }
    }

    public void RecordFailure(string email)
    {
        var key = Normalize(email);
        lock (_lock)
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
    }

    public void Reset(string email)
    {
        lock (_lock)
        {
            _failures.Remove(Normalize(email));
        }
    }

    public void Dispose() => _failures.Dispose();

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

    // 大文字・小文字や前後の空白を変えるだけで制限を回避できないよう、正規化してから数える
    private static string Normalize(string email) => email.Trim().ToLowerInvariant();

    private sealed class FailureEntry(DateTimeOffset windowStart)
    {
        public DateTimeOffset WindowStart { get; } = windowStart;
        public int Count { get; set; }
    }
}
