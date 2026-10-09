namespace TaskManagementSystem.Api.Models;

/// <summary>
/// Git ユーザー⇄アプリメンバーの対応付け(identity マッピング。Phase 2 M4 §6)。
/// Webhook の actor を本人に帰属させ、通知や自動遷移の作者を正しくするために使う。
/// </summary>
public class GitIdentity
{
    public long Id { get; set; }
    public long WorkspaceId { get; set; }
    public long UserId { get; set; }

    public string Provider { get; set; } = null!;

    /// <summary>プロバイダ側の安定したユーザー ID。</summary>
    public string ExternalUserId { get; set; } = null!;

    /// <summary>プロバイダ側のユーザー名(表示・突合用)。</summary>
    public string? ExternalUsername { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public Workspace Workspace { get; set; } = null!;
    public User User { get; set; } = null!;
}
