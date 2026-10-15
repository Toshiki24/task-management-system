using System.Text.Json;

namespace TaskManagementSystem.Api.Services.Sso;

/// <summary>OIDC ディスカバリで得られるエンドポイント群(Phase 2 M5 §7)。</summary>
public record OidcEndpoints(string Issuer, string AuthorizationEndpoint, string TokenEndpoint, string JwksUri);

/// <summary>トークン交換の結果(必要な id_token のみ扱う)。</summary>
public record OidcTokenResult(string IdToken, string? AccessToken);

/// <summary>ID トークンから取り出したユーザー情報。</summary>
public record OidcUserInfo(string Subject, string? Email, bool EmailVerified, string? Name);

/// <summary>OIDC ディスカバリ要求が失敗したことを表す。</summary>
public class OidcException : Exception
{
    public OidcException(string message) : base(message)
    {
    }

    public OidcException(string message, Exception inner) : base(message, inner)
    {
    }
}

/// <summary>
/// OIDC ディスカバリ文書(.well-known/openid-configuration)のパース(Phase 2 M5 §7)。
/// ネットワークに出ない純粋処理のため単体テストで検証する。必須項目が欠けていれば例外。
/// </summary>
public static class OidcDiscoveryParser
{
    public static OidcEndpoints Parse(string json)
    {
        JsonElement root;
        try
        {
            using var doc = JsonDocument.Parse(json);
            root = doc.RootElement.Clone();
        }
        catch (JsonException ex)
        {
            throw new OidcException("OIDC ディスカバリ文書の解析に失敗しました。", ex);
        }

        var issuer = GetRequiredString(root, "issuer");
        var authorization = GetRequiredString(root, "authorization_endpoint");
        var token = GetRequiredString(root, "token_endpoint");
        var jwks = GetRequiredString(root, "jwks_uri");

        return new OidcEndpoints(issuer, authorization, token, jwks);
    }

    private static string GetRequiredString(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var value)
            || value.ValueKind != JsonValueKind.String
            || string.IsNullOrWhiteSpace(value.GetString()))
        {
            throw new OidcException($"OIDC ディスカバリ文書に {name} がありません。");
        }

        return value.GetString()!;
    }
}
