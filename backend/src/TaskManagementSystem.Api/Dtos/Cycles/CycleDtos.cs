using System.ComponentModel.DataAnnotations;
using TaskManagementSystem.Api.Models;

namespace TaskManagementSystem.Api.Dtos.Cycles;

/// <summary>サイクルの進捗(完了数/全数/見積合計)。完了は状態カテゴリ DONE/CANCELLED(M3 §8)。</summary>
public record CycleProgress(int Done, int Total, int Points);

/// <summary>サイクル 1 件(M3 §8)。</summary>
public record CycleDto(
    long Id,
    long ProjectId,
    string Name,
    DateOnly? StartDate,
    DateOnly? EndDate,
    string Status,
    CycleProgress Progress);

/// <summary>サイクルの作成・更新。</summary>
public record CycleRequest(
    [Required(ErrorMessage = "サイクル名は必須です。")]
    [MaxLength(100, ErrorMessage = "サイクル名は100文字以内で入力してください。")]
    string Name,

    DateOnly? StartDate,
    DateOnly? EndDate,

    [AllowedValues(CycleStatus.Planned, CycleStatus.Active, CycleStatus.Closed,
        ErrorMessage = "サイクルの状態が不正です。")]
    string Status = CycleStatus.Planned);

/// <summary>タスクのサイクル割り当て(null=バックログへ戻す)。</summary>
public record AssignCycleRequest(long? CycleId);
