namespace TaskManagementSystem.Api.Models;

/// <summary>ワークスペース内のロール(Phase 2 M1 基本設計 §3)。</summary>
public static class WorkspaceMemberRole
{
    /// <summary>ワークスペースの管理(設定・メンバー・招待)が可能。</summary>
    public const string Admin = "ADMIN";

    /// <summary>プロジェクト/タスク/コメントの作成・編集が可能。</summary>
    public const string Member = "MEMBER";

    /// <summary>閲覧のみ(常に読み取り専用)。</summary>
    public const string Viewer = "VIEWER";
}

/// <summary>
/// ユーザーのワークスペース所属とワークスペース内ロール。
/// 1 ユーザーは複数ワークスペースに所属できる(= (workspace_id, user_id) で一意、user_id 単独では一意にしない)。
/// </summary>
public class WorkspaceMember
{
    public long Id { get; set; }
    public long WorkspaceId { get; set; }
    public long UserId { get; set; }
    public string Role { get; set; } = WorkspaceMemberRole.Member;
    public DateTime CreatedAt { get; set; }

    public Workspace Workspace { get; set; } = null!;
    public User User { get; set; } = null!;
}
