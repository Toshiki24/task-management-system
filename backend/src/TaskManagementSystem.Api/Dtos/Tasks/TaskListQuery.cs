namespace TaskManagementSystem.Api.Dtos.Tasks;

/// <summary>
/// タスク一覧の検索・絞り込み・並べ替え条件(M2 §5.1)。クエリ文字列からバインドする。
/// すべて任意。未指定なら従来どおり ID 昇順で全件返す。
/// </summary>
public class TaskListQuery
{
    /// <summary>状態キー(複数指定で OR)。</summary>
    public string[]? Status { get; set; }

    /// <summary>担当者。数値 ID、"me"(自分)、"none"(未割り当て)のいずれか。</summary>
    public string? AssigneeId { get; set; }

    /// <summary>優先度(複数指定で OR)。</summary>
    public string[]? Priority { get; set; }

    /// <summary>ラベル ID(複数指定で、いずれかを含むタスク)。</summary>
    public long[]? LabelId { get; set; }

    /// <summary>タイトル・説明の部分一致(大文字小文字を区別しない)。</summary>
    public string? Keyword { get; set; }

    /// <summary>期限がこの日以前。</summary>
    public DateOnly? DueBefore { get; set; }

    /// <summary>期限がこの日以降。</summary>
    public DateOnly? DueAfter { get; set; }

    /// <summary>並べ替えキー。先頭に "-" で降順。許可: dueDate/priority/title/createdAt/updatedAt/status。</summary>
    public string? Sort { get; set; }
}
