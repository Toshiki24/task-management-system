using System.IdentityModel.Tokens.Jwt;
using System.Security.Cryptography;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using TaskManagementSystem.Api.Models;
using TaskManagementSystem.Api.Services.Git;

namespace TaskManagementSystem.Api.Tests.Services;

public class GitSecretAndAppAuthTests
{
    private static IConfiguration Config(params (string Key, string Value)[] values) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(values.Select(v => new KeyValuePair<string, string?>(v.Key, v.Value)))
            .Build();

    [Fact(DisplayName = "M4 ConfigSecretStore: Git:Secrets:{ref} を設定から解決する")]
    public async Task ConfigSecretStore_ResolvesFromConfig()
    {
        var store = new ConfigSecretStore(Config(("Git:Secrets:tms/git/acme", "real-secret")), allowRefAsSecret: false);

        Assert.Equal("real-secret", await store.GetSecretAsync("tms/git/acme"));
    }

    [Fact(DisplayName = "M4 ConfigSecretStore: 本番(allowRefAsSecret=false)で未設定なら null")]
    public async Task ConfigSecretStore_NullWhenMissingInProd()
    {
        var store = new ConfigSecretStore(Config(), allowRefAsSecret: false);

        Assert.Null(await store.GetSecretAsync("missing"));
        Assert.Null(await store.GetSecretAsync(null));
    }

    [Fact(DisplayName = "M4 ConfigSecretStore: 開発(allowRefAsSecret=true)は参照を秘密として扱う")]
    public async Task ConfigSecretStore_RefAsSecretInDev()
    {
        var store = new ConfigSecretStore(Config(), allowRefAsSecret: true);

        Assert.Equal("some-ref", await store.GetSecretAsync("some-ref"));
    }

    [Fact(DisplayName = "M4 署名シークレット解決は接続の secret_ref を使う")]
    public async Task WebhookSecretResolver_UsesConnectionRef()
    {
        var store = new ConfigSecretStore(Config(("Git:Secrets:ref1", "sek")), allowRefAsSecret: false);
        var resolver = new SecretStoreWebhookSecretResolver(store);
        var connection = new GitConnection { SecretRef = "ref1" };

        Assert.Equal("sek", await resolver.ResolveSigningSecretAsync(connection));
    }

    [Fact(DisplayName = "M4 GitHub App JWT: iss=App ID・iat/exp を持ち、公開鍵で検証できる")]
    public void GitHubAppJwt_BuildsVerifiableToken()
    {
        using var rsa = RSA.Create(2048);
        var pem = rsa.ExportRSAPrivateKeyPem();
        var now = DateTimeOffset.UtcNow;

        var jwt = GitHubAppJwt.Build("123456", pem, now);

        var handler = new JwtSecurityTokenHandler();
        var parameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = "123456",
            ValidateAudience = false,
            ValidateLifetime = true,
            IssuerSigningKey = new RsaSecurityKey(rsa.ExportParameters(false)),
            ValidateIssuerSigningKey = true,
            ClockSkew = TimeSpan.FromMinutes(1),
        };

        var principal = handler.ValidateToken(jwt, parameters, out var validated);
        Assert.NotNull(principal);
        var token = (JwtSecurityToken)validated;
        Assert.Equal("123456", token.Issuer);
        Assert.Contains(token.Claims, c => c.Type == "iat");
        Assert.True(token.ValidTo > now.UtcDateTime);
        // GitHub の上限(10分)以内
        Assert.True(token.ValidTo <= now.AddMinutes(10).UtcDateTime);
    }
}
