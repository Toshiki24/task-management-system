namespace TaskManagementSystem.Api.Models;

/// <summary>対応する Git プロバイダ(Phase 2 M4 §3)。</summary>
public static class GitProvider
{
    public const string GitHub = "GITHUB";
    public const string GitLab = "GITLAB";

    public static readonly string[] All = { GitHub, GitLab };
}

/// <summary>Git 接続の認証種別(Phase 2 M4 §4)。</summary>
public static class GitAuthType
{
    /// <summary>GitHub App(インストール単位の短命トークン。推奨)。</summary>
    public const string GitHubApp = "GITHUB_APP";
    public const string OAuth = "OAUTH";
    /// <summary>Personal Access Token。</summary>
    public const string Pat = "PAT";
    /// <summary>GitLab の Project/Group アクセストークン。</summary>
    public const string GroupToken = "GROUP_TOKEN";

    public static readonly string[] All = { GitHubApp, OAuth, Pat, GroupToken };
}

/// <summary>Git 接続の状態(Phase 2 M4 §4)。</summary>
public static class GitConnectionStatus
{
    public const string Active = "ACTIVE";
    public const string Disabled = "DISABLED";
    public const string Error = "ERROR";

    public static readonly string[] All = { Active, Disabled, Error };
}

/// <summary>
/// ワークスペース単位の Git 接続(Phase 2 M4 §4)。プロバイダ・接続先・認証種別を保持する。
/// 資格情報そのものは DB に置かず、Secrets Manager 等の参照(<see cref="SecretRef"/>)だけを持つ。
/// </summary>
public class GitConnection
{
    public long Id { get; set; }
    public long WorkspaceId { get; set; }

    /// <summary>プロバイダ(GITHUB/GITLAB)。</summary>
    public string Provider { get; set; } = null!;

    /// <summary>接続先のベース URL。self-managed GitLab 等で使用。null は各プロバイダの既定(github.com / gitlab.com)。</summary>
    public string? BaseUrl { get; set; }

    /// <summary>認証種別(GITHUB_APP/OAUTH/PAT/GROUP_TOKEN)。</summary>
    public string AuthType { get; set; } = null!;

    /// <summary>資格情報のシークレット参照(Secrets Manager の名前/ARN 等)。平文は保持しない。</summary>
    public string? SecretRef { get; set; }

    /// <summary>対象のアカウント/組織/グループ(owner/org/group)。</summary>
    public string? ExternalAccount { get; set; }

    public string Status { get; set; } = GitConnectionStatus.Active;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public Workspace Workspace { get; set; } = null!;
}
