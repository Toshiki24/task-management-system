using System.ComponentModel.DataAnnotations;
using TaskManagementSystem.Api.Models;

namespace TaskManagementSystem.Api.Dtos.Workspaces;

/// <summary>既存ユーザーをワークスペースに追加するリクエスト(= ユーザー追加 API)。</summary>
public record AddWorkspaceMemberRequest(
    [Required(ErrorMessage = "ユーザーIDは必須です。")]
    long? UserId,

    [Required(ErrorMessage = "ワークスペースロールは必須です。")]
    [AllowedValues(null, WorkspaceMemberRole.Admin, WorkspaceMemberRole.Member, WorkspaceMemberRole.Viewer,
        ErrorMessage = "ワークスペースロールの値が不正です。")]
    string? Role
);

/// <summary>ワークスペースメンバーのロール変更リクエスト。</summary>
public record UpdateWorkspaceMemberRoleRequest(
    [Required(ErrorMessage = "ワークスペースロールは必須です。")]
    [AllowedValues(null, WorkspaceMemberRole.Admin, WorkspaceMemberRole.Member, WorkspaceMemberRole.Viewer,
        ErrorMessage = "ワークスペースロールの値が不正です。")]
    string? Role
);

public record WorkspaceMemberDto(long UserId, string Name, string Email, string Role);

public record WorkspaceMemberAddedDto(long WorkspaceId, long UserId, string Role);
