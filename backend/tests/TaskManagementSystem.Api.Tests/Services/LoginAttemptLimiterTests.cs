using Microsoft.Extensions.Configuration;
using TaskManagementSystem.Api.Services;
using TaskManagementSystem.Api.Tests.Infrastructure;

namespace TaskManagementSystem.Api.Tests.Services;

public class LoginAttemptLimiterTests
{
    private const int MaxFailedAttempts = 5;
    private const int MaxFailedAttemptsPerIp = 8;
    private static readonly TimeSpan FailureWindow = TimeSpan.FromSeconds(60);

    private readonly ManualTimeProvider _time = new();

    internal static IConfiguration CreateConfiguration() =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["LoginProtection:MaxFailedAttempts"] = MaxFailedAttempts.ToString(),
                ["LoginProtection:MaxFailedAttemptsPerIp"] = MaxFailedAttemptsPerIp.ToString(),
                ["LoginProtection:FailureWindowSeconds"] = FailureWindow.TotalSeconds.ToString(),
            })
            .Build();

    private LoginAttemptLimiter CreateLimiter() => new(CreateConfiguration(), _time);

    private static void Fail(LoginAttemptLimiter limiter, string email, int times)
    {
        for (var i = 0; i < times; i++)
        {
            limiter.RecordFailure(email);
        }
    }

    [Fact(DisplayName = "UT-801 失敗回数が上限未満なら制限されない")]
    public void GetRetryAfter_ReturnsNull_WhenFailuresAreBelowLimit()
    {
        using var limiter = CreateLimiter();
        Fail(limiter, "user@example.test", MaxFailedAttempts - 1);

        Assert.Null(limiter.GetRetryAfter("user@example.test"));
    }

    [Fact(DisplayName = "UT-802 失敗回数が上限に達すると制限され、解除までの残り時間が返る")]
    public void GetRetryAfter_ReturnsRemainingTime_WhenFailuresReachLimit()
    {
        using var limiter = CreateLimiter();
        Fail(limiter, "user@example.test", MaxFailedAttempts - 1);
        _time.Advance(TimeSpan.FromSeconds(20));
        limiter.RecordFailure("user@example.test");

        // 期間は最初の失敗から数えるため、残り時間は 60秒 - 20秒 = 40秒
        Assert.Equal(TimeSpan.FromSeconds(40), limiter.GetRetryAfter("user@example.test"));
    }

    [Fact(DisplayName = "UT-803 最初の失敗から期間が過ぎると制限が解除される")]
    public void GetRetryAfter_ReturnsNull_AfterFailureWindowPasses()
    {
        using var limiter = CreateLimiter();
        Fail(limiter, "user@example.test", MaxFailedAttempts);

        _time.Advance(FailureWindow - TimeSpan.FromSeconds(1));
        Assert.NotNull(limiter.GetRetryAfter("user@example.test"));

        _time.Advance(TimeSpan.FromSeconds(1));
        Assert.Null(limiter.GetRetryAfter("user@example.test"));

        // 解除後は失敗回数も0から数え直す
        Fail(limiter, "user@example.test", MaxFailedAttempts - 1);
        Assert.Null(limiter.GetRetryAfter("user@example.test"));
    }

    [Fact(DisplayName = "UT-804 リセットすると失敗回数が0に戻る")]
    public void Reset_ClearsFailures()
    {
        using var limiter = CreateLimiter();
        Fail(limiter, "user@example.test", MaxFailedAttempts - 1);

        limiter.Reset("user@example.test");
        Fail(limiter, "user@example.test", MaxFailedAttempts - 1);

        Assert.Null(limiter.GetRetryAfter("user@example.test"));
    }

    [Fact(DisplayName = "UT-805 メールアドレスの大文字・小文字と前後の空白を区別しない")]
    public void RecordFailure_NormalizesEmail()
    {
        using var limiter = CreateLimiter();
        limiter.RecordFailure("user@example.test");
        limiter.RecordFailure("USER@EXAMPLE.TEST");
        limiter.RecordFailure(" User@Example.Test ");
        limiter.RecordFailure("user@EXAMPLE.test");
        limiter.RecordFailure("uSeR@example.test");

        Assert.NotNull(limiter.GetRetryAfter("user@example.test"));
    }

    [Fact(DisplayName = "UT-806 他のメールアドレスの失敗回数に影響しない")]
    public void RecordFailure_DoesNotAffectOtherEmails()
    {
        using var limiter = CreateLimiter();
        Fail(limiter, "locked@example.test", MaxFailedAttempts);

        Assert.NotNull(limiter.GetRetryAfter("locked@example.test"));
        Assert.Null(limiter.GetRetryAfter("other@example.test"));
    }

    // 同じIPから、毎回異なるメールアドレスで失敗させる(アカウント単位の上限には達しない)
    private static void FailFromIp(LoginAttemptLimiter limiter, string ip, int times)
    {
        for (var i = 0; i < times; i++)
        {
            limiter.RecordFailure($"spray-{i}@example.test", ip);
        }
    }

    [Fact(DisplayName = "UT-807 同一IPから異なるアカウントへの失敗が上限に達すると、IP単位で制限される")]
    public void GetRetryAfter_LimitsByIp_AcrossDifferentEmails()
    {
        using var limiter = CreateLimiter();
        FailFromIp(limiter, "203.0.113.1", MaxFailedAttemptsPerIp);

        // 一度も失敗していないアカウントでも、同じIPからは制限される(パスワードスプレー対策)
        Assert.NotNull(limiter.GetRetryAfter("fresh@example.test", "203.0.113.1"));
    }

    [Fact(DisplayName = "UT-808 IP単位の制限は、他のIPに影響しない")]
    public void GetRetryAfter_IpLimit_DoesNotAffectOtherIps()
    {
        using var limiter = CreateLimiter();
        FailFromIp(limiter, "203.0.113.1", MaxFailedAttemptsPerIp);

        Assert.Null(limiter.GetRetryAfter("fresh@example.test", "203.0.113.2"));
    }

    [Fact(DisplayName = "UT-809 IPアドレスが null の場合はIP単位で数えない")]
    public void RecordFailure_DoesNotCountByIp_WhenIpIsNull()
    {
        using var limiter = CreateLimiter();
        for (var i = 0; i < MaxFailedAttemptsPerIp; i++)
        {
            limiter.RecordFailure($"spray-{i}@example.test", ipAddress: null);
        }

        Assert.Null(limiter.GetRetryAfter("fresh@example.test", "203.0.113.1"));
    }

    [Fact(DisplayName = "UT-810 IPが上限未満でも、アカウント単位で上限に達すれば制限される")]
    public void GetRetryAfter_StillLimitsByEmail_WhenIpBelowLimit()
    {
        using var limiter = CreateLimiter();
        for (var i = 0; i < MaxFailedAttempts; i++)
        {
            limiter.RecordFailure("victim@example.test", "203.0.113.1");
        }

        // IP単位(上限8)には達していないが、アカウント単位(上限5)で制限される
        Assert.NotNull(limiter.GetRetryAfter("victim@example.test", "203.0.113.1"));
    }

    [Fact(DisplayName = "UT-811 リセットはアカウント単位のみで、IP単位の記録は残る")]
    public void Reset_DoesNotClearIpFailures()
    {
        using var limiter = CreateLimiter();
        FailFromIp(limiter, "203.0.113.1", MaxFailedAttemptsPerIp);

        limiter.Reset("spray-0@example.test");

        Assert.NotNull(limiter.GetRetryAfter("fresh@example.test", "203.0.113.1"));
    }
}
