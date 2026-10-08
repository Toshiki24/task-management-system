namespace TaskManagementSystem.Api.Models;

public class TaskComment
{
    public long Id { get; set; }
    public long TaskId { get; set; }
    public long UserId { get; set; }
    public string Comment { get; set; } = null!;
    public DateTime CreatedAt { get; set; }

    /// <summary>編集済みフラグ(M3 §3)。</summary>
    public bool Edited { get; set; }

    public DateTime UpdatedAt { get; set; }

    /// <summary>論理削除の時刻(M3 §3)。NULL=未削除。削除済みは本文を伏せて「削除されたコメント」と表示する。</summary>
    public DateTime? DeletedAt { get; set; }

    public TaskItem Task { get; set; } = null!;
    public User User { get; set; } = null!;
}
