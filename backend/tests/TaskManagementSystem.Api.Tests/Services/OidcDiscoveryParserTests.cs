using System.Text.Json;
using TaskManagementSystem.Api.Services.Sso;

namespace TaskManagementSystem.Api.Tests.Services;

public class OidcDiscoveryParserTests
{
    private const string ValidDoc = """
    {
      "issuer": "https://idp.example.com",
      "authorization_endpoint": "https://idp.example.com/authorize",
      "token_endpoint": "https://idp.example.com/token",
      "jwks_uri": "https://idp.example.com/jwks",
      "userinfo_endpoint": "https://idp.example.com/userinfo"
    }
    """;

    /// <summary>有効なディスカバリ文書から指定のキーだけを取り除いた JSON を返す。</summary>
    private static string DocWithout(string key)
    {
        var map = JsonSerializer.Deserialize<Dictionary<string, string>>(ValidDoc)!;
        map.Remove(key);
        return JsonSerializer.Serialize(map);
    }

    [Fact(DisplayName = "OIDC ディスカバリ文書から必要なエンドポイントを取り出す")]
    public void Parse_ExtractsEndpoints()
    {
        var endpoints = OidcDiscoveryParser.Parse(ValidDoc);

        Assert.Equal("https://idp.example.com", endpoints.Issuer);
        Assert.Equal("https://idp.example.com/authorize", endpoints.AuthorizationEndpoint);
        Assert.Equal("https://idp.example.com/token", endpoints.TokenEndpoint);
        Assert.Equal("https://idp.example.com/jwks", endpoints.JwksUri);
    }

    [Theory(DisplayName = "必須項目が欠けていれば OidcException")]
    [InlineData("issuer")]
    [InlineData("authorization_endpoint")]
    [InlineData("token_endpoint")]
    [InlineData("jwks_uri")]
    public void Parse_ThrowsWhenRequiredFieldMissing(string missing)
    {
        var ex = Assert.Throws<OidcException>(() => OidcDiscoveryParser.Parse(DocWithout(missing)));
        Assert.Contains(missing, ex.Message);
    }

    [Fact(DisplayName = "不正な JSON は OidcException")]
    public void Parse_ThrowsOnInvalidJson()
    {
        Assert.Throws<OidcException>(() => OidcDiscoveryParser.Parse("{ not json"));
    }
}
