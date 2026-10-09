using System.ComponentModel.DataAnnotations;
using TaskManagementSystem.Api.Models;

namespace TaskManagementSystem.Api.Dtos.Git;

/// <summary>自動遷移ルール 1 件(M4 §8)。</summary>
public record TransitionRuleDto(long Id, string Trigger, string ToStatusKey, bool Enabled);

/// <summary>ルール 1 件の入力。</summary>
public record TransitionRuleInput(
    [Required(ErrorMessage = "トリガは必須です。")]
    [AllowedValues(
        TransitionTrigger.BranchCreated, TransitionTrigger.PrOpened, TransitionTrigger.MrOpened,
        TransitionTrigger.PrMerged, TransitionTrigger.MrMerged,
        ErrorMessage = "トリガが不正です。")]
    string Trigger,

    [Required(ErrorMessage = "遷移先の状態は必須です。")]
    [MaxLength(50, ErrorMessage = "状態キーは50文字以内で入力してください。")]
    string ToStatusKey,

    bool Enabled = true);

/// <summary>スコープ(WS/プロジェクト)のルールをまとめて置き換える。</summary>
public record PutTransitionRulesRequest(
    [Required]
    List<TransitionRuleInput> Rules);
