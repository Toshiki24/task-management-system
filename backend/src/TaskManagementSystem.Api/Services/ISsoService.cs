using TaskManagementSystem.Api.Dtos.Auth;
using TaskManagementSystem.Api.Dtos.Sso;

namespace TaskManagementSystem.Api.Services;

public enum SsoConnectionResult
{
    Success,
    Forbidden,
    NotFound,
    WorkspaceNotFound,
}

public record SsoConnectionOutcome(SsoConnectionResult Result, SsoConnectionDto? Data = null);

public enum DeleteSsoConnectionResult
{
    Success,
    Forbidden,
    NotFound,
}

public enum SsoStartResult
{
    Success,
    NotFound,
    Error,
}

public record SsoStartOutcome(SsoStartResult Result, string? AuthorizationUrl = null);

public enum SsoCallbackResult
{
    Success,
    InvalidState,
    NotProvisioned,
    Error,
}

public record SsoCallbackOutcome(SsoCallbackResult Result, LoginResponse? Response = null);

/// <summary>
/// SSO(OIDC)の管理と認可フロー(Phase 2 M5 §7)。
/// 接続設定の CRUD は System Admin 専用。開始/コールバックは未ログインから使う。
/// </summary>
public interface ISsoService
{
    Task<List<SsoConnectionDto>?> GetConnectionsAsync(long currentUserId);
    Task<SsoConnectionOutcome> CreateConnectionAsync(long currentUserId, CreateSsoConnectionRequest request);
    Task<SsoConnectionOutcome> UpdateConnectionAsync(long currentUserId, long id, UpdateSsoConnectionRequest request);
    Task<DeleteSsoConnectionResult> DeleteConnectionAsync(long currentUserId, long id);

    /// <summary>ログイン画面に出す有効な接続の一覧(公開)。</summary>
    Task<List<PublicSsoConnectionDto>> GetEnabledConnectionsAsync();

    /// <summary>認可を開始し、IdP へのリダイレクト URL を返す。state/PKCE/nonce を保存する。</summary>
    Task<SsoStartOutcome> StartAsync(long connectionId, string redirectUri);

    /// <summary>コールバックを処理し、ユーザーを突合/プロビジョニングしてセッション(トークン)を発行する。</summary>
    Task<SsoCallbackOutcome> CompleteAsync(SsoCallbackRequest request);
}
