namespace TaskManagementSystem.Api.Models;

/// <summary>
/// 進行中の SSO(OIDC)認可リクエストの一時状態(Phase 2 M5 §7)。
/// Authorization Code Flow の state / PKCE / nonce を、ステートレスな API でも
/// コールバック時に照合できるよう短命レコードとして保持する。
/// state 自体は値ではなくハッシュで保存し、1 回使ったら消費済みにする(リプレイ防止)。
/// </summary>
public class SsoAuthRequest
{
    public long Id { get; set; }

    public long ConnectionId { get; set; }

    /// <summary>state(CSRF トークン)の SHA-256 ハッシュ(16進)。コールバックの state から引く。</summary>
    public string StateHash { get; set; } = null!;

    /// <summary>PKCE の code_verifier(トークン交換時に使用)。</summary>
    public string CodeVerifier { get; set; } = null!;

    /// <summary>ID トークンの nonce 照合用。</summary>
    public string Nonce { get; set; } = null!;

    /// <summary>認可時に使った redirect_uri(トークン交換時に一致させる)。</summary>
    public string RedirectUri { get; set; } = null!;

    public DateTime ExpiresAt { get; set; }
    public DateTime? ConsumedAt { get; set; }
    public DateTime CreatedAt { get; set; }

    public SsoConnection Connection { get; set; } = null!;
}
