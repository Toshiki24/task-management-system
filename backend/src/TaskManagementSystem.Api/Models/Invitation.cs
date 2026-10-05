namespace TaskManagementSystem.Api.Models;

/// <summary>
/// ワークスペースへの招待(Phase 2 M1 基本設計 §4)。
/// 招待は必ず 1 つのワークスペースを対象にロールを指定して発行し、受諾者はそのワークスペースにのみ参加する。
/// トークンは平文を保存せず、ハッシュ(SHA-256)のみを保存する(RefreshToken と同方針)。
/// </summary>
public class Invitation
{
    public long Id { get; set; }
    public long WorkspaceId { get; set; }

    /// <summary>招待先のメールアドレス。</summary>
    public string Email { get; set; } = null!;

    /// <summary>受諾時に付与するワークスペースロール(ADMIN/MEMBER/VIEWER)。</summary>
    public string Role { get; set; } = WorkspaceMemberRole.Member;

    /// <summary>招待トークンの SHA-256 ハッシュ(16進数64文字)。平文は保存しない。</summary>
    public string TokenHash { get; set; } = null!;

    public DateTime ExpiresAt { get; set; }

    /// <summary>NULL=未受諾。値あり=受諾済み(一度きり)。</summary>
    public DateTime? AcceptedAt { get; set; }

    public long InvitedBy { get; set; }
    public DateTime CreatedAt { get; set; }

    public Workspace Workspace { get; set; } = null!;
    public User InvitedByUser { get; set; } = null!;
}
