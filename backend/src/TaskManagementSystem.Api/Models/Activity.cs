namespace TaskManagementSystem.Api.Models;

/// <summary>アクティビティの種別(Phase 2 M3 §5)。</summary>
public static class ActivityVerb
{
    public const string Created = "CREATED";
    public const string Updated = "UPDATED";
    public const string Moved = "MOVED";
    public const string Commented = "COMMENTED";
    // Git 連携(Phase 2 M4 §7)
    public const string GitBranchCreated = "GIT_BRANCH_CREATED";
    public const string GitPrOpened = "GIT_PR_OPENED";
    public const string GitPrMerged = "GIT_PR_MERGED";
    public const string GitPrClosed = "GIT_PR_CLOSED";
    public const string GitCommitLinked = "GIT_COMMIT_LINKED";
}

/// <summary>
/// タスク/プロジェクト単位の活動ストリーム(Phase 2 M3 §5)。
/// 誰が(ActorUserId)何をしたか(Verb)を時系列に残す。詳細は Payload(jsonb)に保持する。
/// 既存の TaskStatusHistory とは当面併存し、表示はこちらを優先する。
/// </summary>
public class Activity
{
    public long Id { get; set; }
    public long ProjectId { get; set; }

    /// <summary>対象タスク。NULL=プロジェクト直下の活動。</summary>
    public long? TaskId { get; set; }

    public long ActorUserId { get; set; }
    public string Verb { get; set; } = null!;

    /// <summary>種別ごとの付加情報(JSON)。例: MOVED={from,to}、UPDATED={fields:[...]}。</summary>
    public string? Payload { get; set; }

    public DateTime CreatedAt { get; set; }

    public Project Project { get; set; } = null!;
    public TaskItem? Task { get; set; }
    public User ActorUser { get; set; } = null!;
}
