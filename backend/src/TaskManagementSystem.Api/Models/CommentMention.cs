namespace TaskManagementSystem.Api.Models;

/// <summary>
/// コメント内の @メンション(M3 §3)。本文中の「@表示名」を保存時に解決し、
/// プロジェクトメンバーに限って (comment_id, user_id) として展開する。
/// 後続(通知・自動ウォッチ)の起点になる。
/// </summary>
public class CommentMention
{
    public long CommentId { get; set; }
    public long UserId { get; set; }

    public TaskComment Comment { get; set; } = null!;
    public User User { get; set; } = null!;
}
