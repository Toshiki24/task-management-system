using Microsoft.Extensions.Configuration;
using TaskManagementSystem.Api.Services.Mfa;
using TaskManagementSystem.Api.Tests.Infrastructure;

namespace TaskManagementSystem.Api.Tests.Services.Mfa;

/// <summary>
/// Docker Compose が <c>Mfa__Key: ${MFA_KEY:-}</c> で空文字の環境変数を渡しても、
/// Jwt:Key にフォールバックして動作すること(回帰: 自己ホストで初回ログイン時に落ちた不具合)。
/// </summary>
public class MfaKeyFallbackTests
{
    private const string JwtKey = "self-host-jwt-key-0123456789-0123456789-0123456789";

    private static IConfiguration Config(string? mfaKey, string? jwtKey) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Mfa:Key"] = mfaKey,
                ["Jwt:Key"] = jwtKey,
            })
            .Build();

    [Theory(DisplayName = "Mfa:Key が空文字/未設定でも Jwt:Key で暗号化器を構築でき、往復できる")]
    [InlineData("")]
    [InlineData(null)]
    public void MfaSecretProtector_FallsBackToJwtKey(string? mfaKey)
    {
        var protector = new MfaSecretProtector(Config(mfaKey, JwtKey));

        var cipher = protector.Protect("JBSWY3DPEHPK3PXP");
        Assert.Equal("JBSWY3DPEHPK3PXP", protector.Unprotect(cipher));
    }

    [Theory(DisplayName = "Mfa:Key が空文字/未設定でも Jwt:Key でチャレンジトークンを発行・検証できる")]
    [InlineData("")]
    [InlineData(null)]
    public void MfaChallengeToken_FallsBackToJwtKey(string? mfaKey)
    {
        var service = new MfaChallengeTokenService(Config(mfaKey, JwtKey), new ManualTimeProvider());

        var token = service.Issue(42);
        Assert.Equal(42, service.Validate(token));
    }

    [Fact(DisplayName = "Mfa:Key も Jwt:Key も無ければ従来どおり例外")]
    public void ThrowsWhenBothMissing()
    {
        Assert.Throws<InvalidOperationException>(() => new MfaSecretProtector(Config("", "")));
        Assert.Throws<InvalidOperationException>(
            () => new MfaChallengeTokenService(Config(null, null), new ManualTimeProvider()));
    }
}
