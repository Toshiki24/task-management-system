using TaskManagementSystem.Api.Services.Mfa;

namespace TaskManagementSystem.Api.Tests.Services.Mfa;

public class TotpTests
{
    // RFC 6238 Appendix B のテストベクタ(SHA1)。シークレットは ASCII "12345678901234567890"。
    private const string RfcSecretBase32 = "GEZDGNBVGY3TQOJQGEZDGNBVGY3TQOJQ";

    [Theory(DisplayName = "RFC 6238 のテストベクタと一致する(6桁)")]
    [InlineData(59, "287082")]
    [InlineData(1111111109, "081804")]
    [InlineData(1111111111, "050471")]
    [InlineData(1234567890, "005924")]
    public void Compute_MatchesRfcVectors(long unixSeconds, string expected)
    {
        var at = DateTimeOffset.FromUnixTimeSeconds(unixSeconds);
        Assert.Equal(expected, Totp.Compute(RfcSecretBase32, at));
    }

    [Fact(DisplayName = "正しいコードは検証に成功し、誤ったコードは失敗する")]
    public void Verify_AcceptsValidRejectsInvalid()
    {
        var at = DateTimeOffset.FromUnixTimeSeconds(59);
        Assert.True(Totp.Verify(RfcSecretBase32, "287082", at, window: 0));
        Assert.False(Totp.Verify(RfcSecretBase32, "000000", at, window: 0));
    }

    [Fact(DisplayName = "前後1ステップの時刻ずれを許容する")]
    public void Verify_AllowsAdjacentWindow()
    {
        // 直前ステップ(T=30〜59)のコードを、次ステップの時刻(T=60)でも許容する
        var prev = DateTimeOffset.FromUnixTimeSeconds(59);
        var code = Totp.Compute(RfcSecretBase32, prev);
        var now = DateTimeOffset.FromUnixTimeSeconds(60);

        Assert.True(Totp.Verify(RfcSecretBase32, code, now, window: 1));
        Assert.False(Totp.Verify(RfcSecretBase32, code, now, window: 0));
    }

    [Fact(DisplayName = "生成したシークレットは Base32 として往復でき、検証も通る")]
    public void GenerateSecret_RoundTripsAndVerifies()
    {
        var secret = Totp.GenerateSecret();
        var at = DateTimeOffset.UtcNow;
        var code = Totp.Compute(secret, at);

        Assert.Equal(secret, Base32.Encode(Base32.Decode(secret)).TrimEnd('='));
        Assert.True(Totp.Verify(secret, code, at));
    }

    [Fact(DisplayName = "otpauth URI にシークレットと発行者が含まれる")]
    public void BuildUri_ContainsSecretAndIssuer()
    {
        var uri = Totp.BuildUri("JBSWY3DPEHPK3PXP", "TaskManagementSystem", "user@example.test");

        Assert.StartsWith("otpauth://totp/", uri);
        Assert.Contains("secret=JBSWY3DPEHPK3PXP", uri);
        Assert.Contains("issuer=TaskManagementSystem", uri);
    }
}
