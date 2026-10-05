using System.ComponentModel.DataAnnotations;
using TaskManagementSystem.Api.Models;

namespace TaskManagementSystem.Api.Dtos.Invitations;

/// <summary>ワークスペースへの招待作成リクエスト(対象ワークスペースはパスの {workspaceId})。</summary>
public record CreateInvitationRequest(
    [Required(ErrorMessage = "メールアドレスは必須です。")]
    [EmailAddress(ErrorMessage = "メールアドレスの形式が正しくありません。")]
    string? Email,

    [Required(ErrorMessage = "ワークスペースロールは必須です。")]
    [AllowedValues(null, WorkspaceMemberRole.Admin, WorkspaceMemberRole.Member, WorkspaceMemberRole.Viewer,
        ErrorMessage = "ワークスペースロールの値が不正です。")]
    string? Role
);

/// <summary>招待作成後のレスポンス。トークンはメールでのみ配布するため含めない。</summary>
public record InvitationCreatedDto(
    long Id,
    long WorkspaceId,
    string Email,
    string Role,
    DateTime ExpiresAt
);

/// <summary>招待リンクを開いたときに表示する情報(受諾前の確認用)。</summary>
public record InvitationPreviewDto(
    long WorkspaceId,
    string WorkspaceName,
    string Email,
    string Role,
    bool IsExistingUser
);

/// <summary>
/// 招待の受諾リクエスト。招待先メールの既存アカウントが無い場合のみ Name/Password が必須。
/// </summary>
public record AcceptInvitationRequest(
    string? Name,
    string? Password
);

/// <summary>受諾結果。<paramref name="AccountCreated"/>=true なら新規アカウントを作成した。</summary>
public record AcceptInvitationResultDto(
    long WorkspaceId,
    long UserId,
    bool AccountCreated
);
