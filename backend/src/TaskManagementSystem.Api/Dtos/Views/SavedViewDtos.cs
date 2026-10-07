using System.ComponentModel.DataAnnotations;
using TaskManagementSystem.Api.Models;

namespace TaskManagementSystem.Api.Dtos.Views;

/// <summary>保存ビューの絞り込み・並べ替え条件(既知キーのみ。§5.3)。</summary>
public record SavedViewFilters(
    string[]? Status,
    string? AssigneeId,
    string[]? Priority,
    long[]? LabelId,
    string? Keyword,
    string? Sort
);

public record SavedViewDto(
    long Id,
    long WorkspaceId,
    string Name,
    string ViewType,
    bool IsShared,
    bool IsOwner,
    SavedViewFilters Filters
);

public record SavedViewRequest(
    [Required(ErrorMessage = "ビュー名は必須です。")]
    [MaxLength(100, ErrorMessage = "ビュー名は100文字以内で入力してください。")]
    string Name,

    [AllowedValues(SavedViewType.List, SavedViewType.Board, ErrorMessage = "表示種別の値が不正です。")]
    string ViewType,

    bool IsShared,

    SavedViewFilters? Filters
);
