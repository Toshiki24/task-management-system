using TaskManagementSystem.Api.Configuration;

namespace TaskManagementSystem.Api.Tests.Configuration;

// Secrets Manager への接続は行わず、取得したシークレットの値(JSON)を設定に変換する処理だけを確認する
public class SecretsManagerConfigurationTests
{
    [Fact(DisplayName = "UT-1001 シークレットのJSONを設定キーと値の組に変換する")]
    public void Parse_ReturnsConfigurationValues()
    {
        var values = SecretsManagerConfiguration.Parse(
            """{"Jwt:Key": "secret-key", "ConnectionStrings:DefaultConnection": "Host=db;Password=p@ss"}""");

        Assert.Equal("secret-key", values["Jwt:Key"]);
        Assert.Equal("Host=db;Password=p@ss", values["ConnectionStrings:DefaultConnection"]);
        Assert.Equal(2, values.Count);
    }

    [Fact(DisplayName = "UT-1002 JSONではない値はエラーになり、メッセージに値を含めない")]
    public void Parse_Throws_WhenNotJson()
    {
        var error = Assert.Throws<InvalidOperationException>(
            () => SecretsManagerConfiguration.Parse("plain-secret-value"));

        Assert.DoesNotContain("plain-secret-value", error.Message);
    }

    [Fact(DisplayName = "UT-1003 オブジェクト以外のJSON、文字列以外の値はエラーになる")]
    public void Parse_Throws_WhenNotFlatStringObject()
    {
        Assert.Throws<InvalidOperationException>(() => SecretsManagerConfiguration.Parse("""["a", "b"]"""));

        var error = Assert.Throws<InvalidOperationException>(
            () => SecretsManagerConfiguration.Parse("""{"Jwt:Key": 12345}"""));
        Assert.Contains("Jwt:Key", error.Message);
        Assert.DoesNotContain("12345", error.Message);
    }
}
