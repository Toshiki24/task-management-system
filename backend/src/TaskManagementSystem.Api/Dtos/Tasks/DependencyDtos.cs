using System.ComponentModel.DataAnnotations;

namespace TaskManagementSystem.Api.Dtos.Tasks;

/// <summary>依存関係で結ばれた相手タスクの要約(依存辺 1 本 = 1 件)。</summary>
public record DependencyLinkDto(long DependencyId, long TaskId, string Title, string Status, bool IsClosed);

/// <summary>
/// あるタスクの依存関係一覧。
/// BlockedBy=このタスクを待たせている(先に終わるべき)タスク群、Blocking=このタスクが待たせているタスク群。
/// </summary>
public record TaskDependenciesDto(
    IReadOnlyList<DependencyLinkDto> BlockedBy,
    IReadOnlyList<DependencyLinkDto> Blocking);

/// <summary>
/// 依存関係の追加。経路のタスク(taskId)を基準に、
/// Relation="BLOCKED_BY" なら「taskId は TaskId に待たされる」、"BLOCKS" なら「taskId が TaskId を待たせる」。
/// </summary>
public record AddDependencyRequest(
    [Required(ErrorMessage = "対象タスクは必須です。")]
    long? TaskId,

    string Relation = DependencyRelations.BlockedBy);

/// <summary>依存関係の向き(経路タスクを基準にした相対指定)。</summary>
public static class DependencyRelations
{
    public const string BlockedBy = "BLOCKED_BY";
    public const string Blocks = "BLOCKS";
}
