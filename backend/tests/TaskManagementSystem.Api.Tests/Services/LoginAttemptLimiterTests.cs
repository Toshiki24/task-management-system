using Microsoft.Extensions.Configuration;
using TaskManagementSystem.Api.Services;
using TaskManagementSystem.Api.Tests.Infrastructure;

namespace TaskManagementSystem.Api.Tests.Services;

public class LoginAttemptLimiterTests
{
    private const int MaxFailedAttempts = 5;
    private static readonly TimeSpan FailureWindow = TimeSpan.FromSeconds(60);

    private readonly ManualTimeProvider _time = new();

    internal static IConfiguration CreateConfiguration() =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["LoginProtection:MaxFailedAttempts"] = MaxFailedAttempts.ToString(),
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
}
