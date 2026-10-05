using System.ComponentModel.DataAnnotations;

namespace TaskManagementSystem.Api.Dtos.Workspaces;

/// <summary>ワークスペースの作成・更新リクエスト。</summary>
public record WorkspaceRequest(
    [Required(ErrorMessage = "ワークスペース名は必須です。")]
    [StringLength(100, ErrorMessage = "ワークスペース名は100文字以内で入力してください。")]
    string? Name,

    [StringLength(500, ErrorMessage = "説明は500文字以内で入力してください。")]
    string? Description
);
