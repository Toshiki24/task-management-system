namespace TaskManagementSystem.Api.Models;

/// <summary>保存ビューの表示種別。</summary>
public static class SavedViewType
{
    public const string List = "LIST";
    public const string Board = "BOARD";
}

/// <summary>
/// 保存ビュー(ワークスペース単位)。絞り込み・並べ替え・表示種別の組を保存して再利用する(Phase 2 M2 §5.2)。
/// 個人用(本人のみ)と共有(同一WSの所属者に見える)がある。
/// </summary>
public class SavedView
{
    public long Id { get; set; }
    public long WorkspaceId { get; set; }

    /// <summary>作成者。</summary>
    public long OwnerUserId { get; set; }

    public string Name { get; set; } = null!;

    /// <summary><see cref="SavedViewType"/> のいずれか。</summary>
    public string ViewType { get; set; } = SavedViewType.List;

    /// <summary>true=同一WSの所属者に共有、false=本人のみ。</summary>
    public bool IsShared { get; set; }

    /// <summary>絞り込み・並べ替え条件(JSON。既知キーのみを保存する。§5.3)。</summary>
    public string Filters { get; set; } = "{}";

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public Workspace Workspace { get; set; } = null!;
    public User OwnerUser { get; set; } = null!;
}
