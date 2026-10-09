namespace TaskManagementSystem.Api.Models;

/// <summary>自動遷移のトリガとなる Git イベント(Phase 2 M4 §8)。</summary>
public static class TransitionTrigger
{
    public const string BranchCreated = "BRANCH_CREATED";
    public const string PrOpened = "PR_OPENED";
    public const string MrOpened = "MR_OPENED";
    public const string PrMerged = "PR_MERGED";
    public const string MrMerged = "MR_MERGED";

    public static readonly string[] All = { BranchCreated, PrOpened, MrOpened, PrMerged, MrMerged };
}

/// <summary>
/// タスク自動遷移のルール(Phase 2 M4 §8)。トリガとなる Git イベントごとに遷移先の状態を定める。
/// ProjectId が null の行はワークスペース既定、値があればそのプロジェクトで上書きする。
/// </summary>
public class TransitionRule
{
    public long Id { get; set; }
    public long WorkspaceId { get; set; }

    /// <summary>null=ワークスペース既定。値あり=そのプロジェクトの上書き。</summary>
    public long? ProjectId { get; set; }

    /// <summary>トリガ(BRANCH_CREATED/PR_OPENED/...)。</summary>
    public string Trigger { get; set; } = null!;

    /// <summary>遷移先の状態キー(workflow_states.key)。</summary>
    public string ToStatusKey { get; set; } = null!;

    public bool Enabled { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public Workspace Workspace { get; set; } = null!;
    public Project? Project { get; set; }
}
