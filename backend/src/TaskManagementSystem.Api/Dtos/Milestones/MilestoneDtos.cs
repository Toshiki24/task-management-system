using System.ComponentModel.DataAnnotations;
using TaskManagementSystem.Api.Models;

namespace TaskManagementSystem.Api.Dtos.Milestones;

/// <summary>マイルストーンの進捗(完了数/全数/見積合計)。完了は状態カテゴリ DONE/CANCELLED(M3 §8)。</summary>
public record MilestoneProgress(int Done, int Total, int Points);

/// <summary>マイルストーン 1 件(M3 §8)。</summary>
public record MilestoneDto(
    long Id,
    long ProjectId,
    string Name,
    DateOnly? DueDate,
    string Status,
    MilestoneProgress Progress);

/// <summary>マイルストーンの作成・更新。</summary>
public record MilestoneRequest(
    [Required(ErrorMessage = "マイルストーン名は必須です。")]
    [MaxLength(100, ErrorMessage = "マイルストーン名は100文字以内で入力してください。")]
    string Name,

    DateOnly? DueDate,

    [AllowedValues(MilestoneStatus.Open, MilestoneStatus.Closed,
        ErrorMessage = "マイルストーンの状態が不正です。")]
    string Status = MilestoneStatus.Open);

/// <summary>タスクのマイルストーン割り当て(null=未割り当てへ戻す)。</summary>
public record AssignMilestoneRequest(long? MilestoneId);
