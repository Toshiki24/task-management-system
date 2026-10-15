using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace TaskManagementSystem.Api.Services.Sso;

/// <summary>
/// 実際の OIDC プロバイダと通信する <see cref="IOidcClient"/> 実装(Phase 2 M5 §7)。
/// ディスカバリ・トークン交換は HTTP、ID トークン検証は JWKS による署名検証を行う。
/// ネットワークに出るため単体テストでは <see cref="IOidcClient"/> をフェイクに差し替える。
/// </summary>
public class HttpOidcClient : IOidcClient
{
    private readonly HttpClient _httpClient;

    public HttpOidcClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<OidcEndpoints> GetEndpointsAsync(string issuer, CancellationToken ct = default)
    {
        var url = $"{issuer.TrimEnd('/')}/.well-known/openid-configuration";
        string json;
        try
        {
            json = await _httpClient.GetStringAsync(url, ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            throw new OidcException("OIDC ディスカバリの取得に失敗しました。", ex);
        }

        return OidcDiscoveryParser.Parse(json);
    }

    public async Task<OidcTokenResult> ExchangeCodeAsync(
        OidcEndpoints endpoints,
        string clientId,
        string? clientSecret,
        string code,
        string redirectUri,
        string codeVerifier,
        CancellationToken ct = default)
    {
        var form = new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["code"] = code,
            ["redirect_uri"] = redirectUri,
            ["client_id"] = clientId,
            ["code_verifier"] = codeVerifier,
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, endpoints.TokenEndpoint)
        {
            Content = new FormUrlEncodedContent(form),
        };
        // confidential client は HTTP Basic 認証でクライアント資格情報を送る
        if (!string.IsNullOrEmpty(clientSecret))
        {
            var basic = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{clientId}:{clientSecret}"));
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", basic);
        }

        HttpResponseMessage response;
        try
        {
            response = await _httpClient.SendAsync(request, ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            throw new OidcException("OIDC トークン交換の通信に失敗しました。", ex);
        }

        var body = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
        {
            throw new OidcException($"OIDC トークン交換に失敗しました(HTTP {(int)response.StatusCode})。");
        }

        string? idToken;
        string? accessToken;
        try
        {
            using var doc = JsonDocument.Parse(body);
            idToken = doc.RootElement.TryGetProperty("id_token", out var it) ? it.GetString() : null;
            accessToken = doc.RootElement.TryGetProperty("access_token", out var at) ? at.GetString() : null;
        }
        catch (JsonException ex)
        {
            throw new OidcException("OIDC トークン応答の解析に失敗しました。", ex);
        }

        if (string.IsNullOrEmpty(idToken))
        {
            throw new OidcException("OIDC トークン応答に id_token がありません。");
        }

        return new OidcTokenResult(idToken, accessToken);
    }

    public async Task<OidcUserInfo> ValidateIdTokenAsync(
        OidcEndpoints endpoints,
        string idToken,
        string clientId,
        string expectedNonce,
        CancellationToken ct = default)
    {
        string jwksJson;
        try
        {
            jwksJson = await _httpClient.GetStringAsync(endpoints.JwksUri, ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            throw new OidcException("OIDC の署名鍵(JWKS)の取得に失敗しました。", ex);
        }

        var keys = new JsonWebKeySet(jwksJson).GetSigningKeys();
        var handler = new JsonWebTokenHandler();
        var result = await handler.ValidateTokenAsync(idToken, new TokenValidationParameters
        {
            ValidIssuer = endpoints.Issuer,
            ValidAudience = clientId,
            IssuerSigningKeys = keys,
            ValidateIssuerSigningKey = true,
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
        });

        if (!result.IsValid)
        {
            throw new OidcException("ID トークンの検証に失敗しました。");
        }

        var claims = result.Claims;
        var nonce = GetClaimString(claims, "nonce");
        if (!string.Equals(nonce, expectedNonce, StringComparison.Ordinal))
        {
            throw new OidcException("ID トークンの nonce が一致しません。");
        }

        var subject = GetClaimString(claims, "sub")
            ?? throw new OidcException("ID トークンに sub がありません。");
        var email = GetClaimString(claims, "email");
        var name = GetClaimString(claims, "name");
        var emailVerified = GetClaimBool(claims, "email_verified");

        return new OidcUserInfo(subject, email, emailVerified, name);
    }

    private static string? GetClaimString(IDictionary<string, object> claims, string name) =>
        claims.TryGetValue(name, out var value) ? value?.ToString() : null;

    private static bool GetClaimBool(IDictionary<string, object> claims, string name)
    {
        if (!claims.TryGetValue(name, out var value) || value is null)
        {
            // email_verified が無い IdP もあるため、欠落時は検証済み扱いにはせず true とする
            return true;
        }

        return value switch
        {
            bool b => b,
            string s => bool.TryParse(s, out var parsed) && parsed,
            _ => false,
        };
    }
}
