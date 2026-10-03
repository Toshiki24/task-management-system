using Microsoft.Extensions.Configuration;
using TaskManagementSystem.Api.Services;

namespace TaskManagementSystem.Api.Tests.Services;

// X-Origin-Verify の共有シークレットの読み込み(強度チェック・ローテーション)と照合を確認する(security-review-2.md SEC2-01 / SEC2-02)
public class OriginVerifyTests
{
    private static IConfiguration ConfigurationWith(string? secret, string? previousSecret = null)
    {
        var values = new Dictionary<string, string?>();
        if (secret is not null)
        {
            values["OriginVerify:Secret"] = secret;
        }

        if (previousSecret is not null)
        {
            values["OriginVerify:PreviousSecret"] = previousSecret;
        }

        return new ConfigurationBuilder().AddInMemoryCollection(values).Build();
    }

    [Fact(DisplayName = "UT-1101 設定された共有シークレットを読み込む")]
    public void ResolveSecrets_ReturnsConfiguredValue()
    {
        var secret = new string('a', OriginVerify.MinimumBytes);

        Assert.Equal(new[] { secret }, OriginVerify.ResolveSecrets(ConfigurationWith(secret), isProduction: true));
    }

    [Fact(DisplayName = "UT-1102 本番で未設定なら起動時エラーにする")]
    public void ResolveSecrets_Throws_WhenMissingInProduction()
    {
        Assert.Throws<InvalidOperationException>(
            () => OriginVerify.ResolveSecrets(ConfigurationWith(null), isProduction: true));
    }

    [Fact(DisplayName = "UT-1103 本番で短すぎる場合は起動時エラーにする")]
    public void ResolveSecrets_Throws_WhenTooShortInProduction()
    {
        var error = Assert.Throws<InvalidOperationException>(
            () => OriginVerify.ResolveSecrets(ConfigurationWith("short"), isProduction: true));

        Assert.Contains($"{OriginVerify.MinimumBytes}", error.Message);
    }

    [Fact(DisplayName = "UT-1104 本番以外で未設定なら空(検証しない)")]
    public void ResolveSecrets_ReturnsEmpty_WhenMissingOutsideProduction()
    {
        Assert.Empty(OriginVerify.ResolveSecrets(ConfigurationWith(null), isProduction: false));
    }

    [Fact(DisplayName = "UT-1105 一致する値は true")]
    public void IsValid_ReturnsTrue_WhenMatches()
    {
        Assert.True(OriginVerify.IsValid("the-shared-secret", new[] { "the-shared-secret" }));
    }

    [Theory(DisplayName = "UT-1106 一致しない・未指定の値は false")]
    [InlineData("wrong-secret")]
    [InlineData("")]
    [InlineData(null)]
    public void IsValid_ReturnsFalse_WhenDoesNotMatch(string? provided)
    {
        Assert.False(OriginVerify.IsValid(provided, new[] { "the-shared-secret" }));
    }

    [Fact(DisplayName = "UT-1107 ローテーション中は現行・旧値のどちらも受け付ける")]
    public void ResolveSecrets_IncludesPreviousSecret_ForRotation()
    {
        var current = new string('a', OriginVerify.MinimumBytes);
        var previous = new string('b', OriginVerify.MinimumBytes);

        var secrets = OriginVerify.ResolveSecrets(ConfigurationWith(current, previous), isProduction: true);

        Assert.Equal(new[] { current, previous }, secrets);
        Assert.True(OriginVerify.IsValid(current, secrets));
        Assert.True(OriginVerify.IsValid(previous, secrets));
        Assert.False(OriginVerify.IsValid("other", secrets));
    }

    [Fact(DisplayName = "UT-1108 本番で旧値が短すぎる場合も起動時エラーにする")]
    public void ResolveSecrets_Throws_WhenPreviousSecretTooShortInProduction()
    {
        var current = new string('a', OriginVerify.MinimumBytes);

        Assert.Throws<InvalidOperationException>(
            () => OriginVerify.ResolveSecrets(ConfigurationWith(current, "short"), isProduction: true));
    }

    [Fact(DisplayName = "UT-1109 空の候補一覧には一致しない")]
    public void IsValid_ReturnsFalse_WhenNoAcceptedSecrets()
    {
        Assert.False(OriginVerify.IsValid("anything", Array.Empty<string>()));
    }
}
