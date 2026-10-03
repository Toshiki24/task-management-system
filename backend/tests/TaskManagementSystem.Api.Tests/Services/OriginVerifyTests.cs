using Microsoft.Extensions.Configuration;
using TaskManagementSystem.Api.Services;

namespace TaskManagementSystem.Api.Tests.Services;

// X-Origin-Verify の共有シークレットの読み込み(強度チェック)と照合を確認する(security-review-2.md SEC2-01 / SEC2-02)
public class OriginVerifyTests
{
    private static IConfiguration ConfigurationWith(string? secret)
    {
        var values = new Dictionary<string, string?>();
        if (secret is not null)
        {
            values["OriginVerify:Secret"] = secret;
        }

        return new ConfigurationBuilder().AddInMemoryCollection(values).Build();
    }

    [Fact(DisplayName = "UT-1101 設定された共有シークレットを読み込む")]
    public void ResolveSecret_ReturnsConfiguredValue()
    {
        var secret = new string('a', OriginVerify.MinimumBytes);

        Assert.Equal(secret, OriginVerify.ResolveSecret(ConfigurationWith(secret), isProduction: true));
    }

    [Fact(DisplayName = "UT-1102 本番で未設定なら起動時エラーにする")]
    public void ResolveSecret_Throws_WhenMissingInProduction()
    {
        Assert.Throws<InvalidOperationException>(
            () => OriginVerify.ResolveSecret(ConfigurationWith(null), isProduction: true));
    }

    [Fact(DisplayName = "UT-1103 本番で短すぎる場合は起動時エラーにする")]
    public void ResolveSecret_Throws_WhenTooShortInProduction()
    {
        var error = Assert.Throws<InvalidOperationException>(
            () => OriginVerify.ResolveSecret(ConfigurationWith("short"), isProduction: true));

        Assert.Contains($"{OriginVerify.MinimumBytes}", error.Message);
    }

    [Fact(DisplayName = "UT-1104 本番以外で未設定なら null(検証しない)")]
    public void ResolveSecret_ReturnsNull_WhenMissingOutsideProduction()
    {
        Assert.Null(OriginVerify.ResolveSecret(ConfigurationWith(null), isProduction: false));
    }

    [Fact(DisplayName = "UT-1105 一致する値は true")]
    public void IsValid_ReturnsTrue_WhenMatches()
    {
        Assert.True(OriginVerify.IsValid("the-shared-secret", "the-shared-secret"));
    }

    [Theory(DisplayName = "UT-1106 一致しない・未指定の値は false")]
    [InlineData("wrong-secret")]
    [InlineData("")]
    [InlineData(null)]
    public void IsValid_ReturnsFalse_WhenDoesNotMatch(string? provided)
    {
        Assert.False(OriginVerify.IsValid(provided, "the-shared-secret"));
    }
}
