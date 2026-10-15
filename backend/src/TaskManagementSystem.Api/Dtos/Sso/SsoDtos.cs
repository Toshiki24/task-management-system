using System.ComponentModel.DataAnnotations;

namespace TaskManagementSystem.Api.Dtos.Sso;

/// <summary>SSO 接続の管理用表現(System Admin 向け)。シークレットそのものは返さない。</summary>
public record SsoConnectionDto(
    long Id,
    string Name,
    string Protocol,
    string Issuer,
    string ClientId,
    string Scopes,
    bool AutoProvision,
    long? ProvisionWorkspaceId,
    string? ProvisionRole,
    bool IsEnabled,
    bool HasSecret);

/// <summary>ログイン画面に出す公開情報(最小限)。</summary>
public record PublicSsoConnectionDto(long Id, string Name);

/// <summary>SSO 接続の作成リクエスト。</summary>
public record CreateSsoConnectionRequest(
    [Required][MaxLength(100)] string Name,
    [Required][MaxLength(300)] string Issuer,
    [Required][MaxLength(300)] string ClientId,
    [MaxLength(300)] string? SecretRef,
    [MaxLength(300)] string? Scopes,
    bool AutoProvision,
    long? ProvisionWorkspaceId,
    [AllowedValues(null, "ADMIN", "MEMBER", "VIEWER")] string? ProvisionRole);

/// <summary>SSO 接続の更新リクエスト。SecretRef は null なら変更しない(空文字で消去)。</summary>
public record UpdateSsoConnectionRequest(
    [Required][MaxLength(100)] string Name,
    [Required][MaxLength(300)] string Issuer,
    [Required][MaxLength(300)] string ClientId,
    [MaxLength(300)] string? SecretRef,
    [MaxLength(300)] string? Scopes,
    bool AutoProvision,
    long? ProvisionWorkspaceId,
    [AllowedValues(null, "ADMIN", "MEMBER", "VIEWER")] string? ProvisionRole,
    bool IsEnabled);

/// <summary>SSO 認可の開始結果(IdP への遷移先 URL)。</summary>
public record SsoStartDto(string AuthorizationUrl);

/// <summary>SSO コールバックのリクエスト(BFF から中継)。</summary>
public record SsoCallbackRequest(
    [Required] string State,
    [Required] string Code);
