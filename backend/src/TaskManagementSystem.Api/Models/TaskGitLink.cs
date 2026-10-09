namespace TaskManagementSystem.Api.Models;

/// <summary>Git リンクの種別(Phase 2 M4 §7)。</summary>
public static class GitLinkType
{
    public const string Branch = "BRANCH";
    public const string PullRequest = "PR";
    public const string MergeRequest = "MR";
    public const string Commit = "COMMIT";

    public static readonly string[] All = { Branch, PullRequest, MergeRequest, Commit };
}

/// <summary>PR/MR の状態(Phase 2 M4 §7)。ブランチ/コミットでは null。</summary>
public static class GitLinkState
{
    public const string Open = "OPEN";
    public const string Merged = "MERGED";
    public const string Closed = "CLOSED";

    public static readonly string[] All = { Open, Merged, Closed };
}

/// <summary>
/// タスク⇄Git オブジェクト(ブランチ/PR/MR/コミット)の双方向リンク(Phase 2 M4 §7)。
/// </summary>
public class TaskGitLink
{
    public long Id { get; set; }
    public long TaskId { get; set; }
    public long RepositoryLinkId { get; set; }

    /// <summary>リンク種別(BRANCH/PR/MR/COMMIT)。</summary>
    public string LinkType { get; set; } = null!;

    /// <summary>プロバイダ側の参照(ブランチ名 / PR・MR 番号 / コミット SHA)。</summary>
    public string ExternalRef { get; set; } = null!;

    public string? Url { get; set; }
    public string? Title { get; set; }

    /// <summary>PR/MR の状態(OPEN/MERGED/CLOSED)。ブランチ/コミットは null。</summary>
    public string? State { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public TaskItem Task { get; set; } = null!;
    public RepositoryLink RepositoryLink { get; set; } = null!;
}
