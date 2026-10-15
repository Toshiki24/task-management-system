namespace TaskManagementSystem.Api.Models;

/// <summary>SSO 接続の方式(Phase 2 M5 §7)。まずは OIDC のみ対応し、SAML は将来拡張。</summary>
public static class SsoProtocol
{
    public const string Oidc = "OIDC";

    public static readonly string[] All = { Oidc };
}

/// <summary>
/// インスタンス全体の SSO(OIDC)接続設定(Phase 2 M5 §7)。System Admin が管理する。
/// クライアントシークレットは DB に平文で置かず、参照(<see cref="SecretRef"/>)から
/// <see cref="Services.Git.ISecretStore"/> 経由で解決する(M4 の方針を再利用)。
/// </summary>
public class SsoConnection
{
    public long Id { get; set; }

    /// <summary>ログイン画面に表示する名称(例: "Google Workspace")。</summary>
    public string Name { get; set; } = null!;

    /// <summary>方式(現状は OIDC)。</summary>
    public string Protocol { get; set; } = SsoProtocol.Oidc;

    /// <summary>OIDC issuer(ディスカバリのベース URL)。例: https://accounts.google.com</summary>
    public string Issuer { get; set; } = null!;

    /// <summary>OIDC クライアント ID。</summary>
    public string ClientId { get; set; } = null!;

    /// <summary>クライアントシークレットの参照(平文は保持しない)。null は public client。</summary>
    public string? SecretRef { get; set; }

    /// <summary>要求するスコープ(スペース区切り)。既定は "openid email profile"。</summary>
    public string Scopes { get; set; } = "openid email profile";

    /// <summary>
    /// 未登録メールの自動プロビジョニング可否。false なら既存ユーザーのみログイン許可。
    /// true のとき、<see cref="ProvisionWorkspaceId"/>/<see cref="ProvisionRole"/> に沿ってユーザーを作成する。
    /// </summary>
    public bool AutoProvision { get; set; }

    /// <summary>自動プロビジョニング時に所属させるワークスペース(任意)。</summary>
    public long? ProvisionWorkspaceId { get; set; }

    /// <summary>自動プロビジョニング時に付与するロール(ADMIN/MEMBER/VIEWER、任意)。</summary>
    public string? ProvisionRole { get; set; }

    public bool IsEnabled { get; set; } = true;

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
