using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.IdentityModel.Tokens;

namespace TaskManagementSystem.Api.Services.Git;

/// <summary>
/// GitHub App 認証(M4 §4/§15 ステップ7)。App の秘密鍵でアプリ JWT(RS256)を作り、
/// インストール ID から短命のインストールトークンを発行する。秘密鍵は Secrets Manager から
/// 取得する(ISecretStore 経由)。個人非依存・最小権限・短命トークンのため GitHub App を推奨。
/// </summary>
public static class GitHubAppJwt
{
    /// <summary>
    /// GitHub App 用の JWT を作る。iss=App ID、iat は時計ずれを見込んで少し過去、有効期限は最大 10 分
    /// (GitHub の上限)。署名は App 秘密鍵(PEM)で RS256。
    /// </summary>
    public static string Build(string appId, string privateKeyPem, DateTimeOffset now)
    {
        using var rsa = RSA.Create();
        rsa.ImportFromPem(privateKeyPem);

        var issuedAt = now.AddSeconds(-30);
        var expires = now.AddMinutes(9);
        var signingCredentials = new SigningCredentials(new RsaSecurityKey(rsa), SecurityAlgorithms.RsaSha256);

        var token = new JwtSecurityToken(
            issuer: appId,
            claims: new[]
            {
                new Claim("iat", issuedAt.ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64),
            },
            notBefore: issuedAt.UtcDateTime,
            expires: expires.UtcDateTime,
            signingCredentials: signingCredentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}

/// <summary>GitHub App のインストールトークンを取得する(本番専用。テストでは使わない)。</summary>
public interface IGitHubAppTokenProvider
{
    /// <summary>インストール ID に対する短命トークンを発行する。</summary>
    Task<string> GetInstallationTokenAsync(
        string appId, string privateKeyPem, string installationId, string? apiBaseUrl = null, CancellationToken ct = default);
}

/// <summary>
/// GitHub REST API でインストールトークンを交換する実装(M4 §15 ステップ7)。外部ネットワークに出るため
/// 単体テストの対象外。接続先は apiBaseUrl(未指定なら api.github.com)。
/// </summary>
public class GitHubAppTokenProvider : IGitHubAppTokenProvider
{
    private readonly IHttpClientFactory _httpClientFactory;

    public GitHubAppTokenProvider(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public async Task<string> GetInstallationTokenAsync(
        string appId, string privateKeyPem, string installationId, string? apiBaseUrl = null, CancellationToken ct = default)
    {
        var jwt = GitHubAppJwt.Build(appId, privateKeyPem, DateTimeOffset.UtcNow);
        var baseUrl = string.IsNullOrWhiteSpace(apiBaseUrl) ? "https://api.github.com" : apiBaseUrl.TrimEnd('/');

        using var client = _httpClientFactory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/app/installations/{installationId}/access_tokens");
        request.Headers.Add("Authorization", $"Bearer {jwt}");
        request.Headers.Add("Accept", "application/vnd.github+json");
        request.Headers.Add("User-Agent", "task-management-system");

        using var response = await client.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync(ct);

        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.GetProperty("token").GetString()
            ?? throw new InvalidOperationException("インストールトークンの取得に失敗しました。");
    }
}
