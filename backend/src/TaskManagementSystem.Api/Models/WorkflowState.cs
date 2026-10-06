namespace TaskManagementSystem.Api.Models;

/// <summary>
/// ワークフロー状態のカテゴリ(カンバンの意味づけ・指標で使う固定集合)。
/// 状態そのもの(列)はワークスペースごとに可変だが、カテゴリは固定。
/// </summary>
public static class WorkflowStateCategory
{
    public const string Backlog = "BACKLOG";
    public const string Todo = "TODO";
    public const string InProgress = "IN_PROGRESS";
    public const string Done = "DONE";
    public const string Cancelled = "CANCELLED";

    public static readonly IReadOnlyList<string> All = new[]
    {
        Backlog, Todo, InProgress, Done, Cancelled,
    };

    /// <summary>完了扱い(一覧の既定表示や指標で「終わったタスク」とみなすカテゴリ)。</summary>
    public static bool IsClosed(string category) =>
        category is Done or Cancelled;
}

/// <summary>既定ワークフローの定義(新規 WS・既存 WS のバックフィルで共通に使う 1 件分)。</summary>
public sealed record WorkflowStateSeed(string Key, string Name, string Category, int Position, bool IsDefault);

/// <summary>
/// 新規ワークスペースに用意する既定の状態。Phase 1 の固定 3 状態(TODO/IN_PROGRESS/DONE)を踏襲し、
/// 既存タスクの status 値がそのまま有効な状態キーになるようにする(Phase 2 M2 §3.1・§12)。
/// </summary>
public static class DefaultWorkflow
{
    public static readonly IReadOnlyList<WorkflowStateSeed> States = new[]
    {
        new WorkflowStateSeed("TODO", "未着手", WorkflowStateCategory.Todo, 0, IsDefault: true),
        new WorkflowStateSeed("IN_PROGRESS", "対応中", WorkflowStateCategory.InProgress, 1, IsDefault: false),
        new WorkflowStateSeed("DONE", "完了", WorkflowStateCategory.Done, 2, IsDefault: false),
    };
}

/// <summary>
/// タスクの状態(カンバンのカラム)。ワークスペース単位で定義し、追加・改名・並べ替えができる(Phase 2 M2 §3)。
/// 固定の TODO/IN_PROGRESS/DONE に縛られないよう、タスクの status はこの key を指す。
/// </summary>
public class WorkflowState
{
    public long Id { get; set; }
    public long WorkspaceId { get; set; }

    /// <summary>安定キー(例 "TODO")。タスクの status 値・履歴に使う。WS 内で一意。</summary>
    public string Key { get; set; } = null!;

    /// <summary>表示名(例「対応中」)。</summary>
    public string Name { get; set; } = null!;

    /// <summary><see cref="WorkflowStateCategory"/> のいずれか。</summary>
    public string Category { get; set; } = WorkflowStateCategory.Todo;

    /// <summary>カンバンの列の並び順。</summary>
    public int Position { get; set; }

    /// <summary>新規タスクの初期状態(WS につき 1 件 true)。</summary>
    public bool IsDefault { get; set; }

    /// <summary>列の色(任意、未指定可)。</summary>
    public string? Color { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public Workspace Workspace { get; set; } = null!;
}
