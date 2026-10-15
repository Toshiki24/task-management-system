namespace TaskManagementSystem.Api.Services.Sso;

/// <summary>
/// OIDC プロバイダとのやり取りの抽象(Phase 2 M5 §7)。
/// 実接続(ディスカバリ・トークン交換・JWKS 検証)は <see cref="HttpOidcClient"/> が担い、
/// オーケストレーション(<see cref="SsoService"/>)はこの抽象に依存することで、
/// 外部ネットワークに出ずに単体テストできるようにする(M4 のプロバイダ抽象と同じ方針)。
/// </summary>
public interface IOidcClient
{
    /// <summary>issuer のディスカバリ文書を取得してエンドポイントを返す。</summary>
    Task<OidcEndpoints> GetEndpointsAsync(string issuer, CancellationToken ct = default);

    /// <summary>認可コードを ID トークンに交換する(PKCE code_verifier を使用)。</summary>
    Task<OidcTokenResult> ExchangeCodeAsync(
        OidcEndpoints endpoints,
        string clientId,
        string? clientSecret,
        string code,
        string redirectUri,
        string codeVerifier,
        CancellationToken ct = default);

    /// <summary>
    /// ID トークンを検証(署名・issuer・audience・有効期限・nonce)し、ユーザー情報を取り出す。
    /// 検証に失敗したら <see cref="OidcException"/>。
    /// </summary>
    Task<OidcUserInfo> ValidateIdTokenAsync(
        OidcEndpoints endpoints,
        string idToken,
        string clientId,
        string expectedNonce,
        CancellationToken ct = default);
}
