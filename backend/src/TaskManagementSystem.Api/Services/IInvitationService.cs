using TaskManagementSystem.Api.Dtos.Invitations;

namespace TaskManagementSystem.Api.Services;

public enum CreateInvitationResult
{
    Success,
    WorkspaceNotFound,
    Forbidden,
    AlreadyMember,
}

public enum AcceptInvitationResult
{
    Success,
    /// <summary>トークンが無効・期限切れ・使用済み(存在を推測させないため同一応答)。</summary>
    Invalid,
    /// <summary>招待先メールの既存アカウントが無く、新規登録情報(名前・パスワード)が必要/不正。</summary>
    RegistrationRequired,
}

public record CreateInvitationOutcome(CreateInvitationResult Result, InvitationCreatedDto? Data = null);

public record AcceptInvitationOutcome(
    AcceptInvitationResult Result,
    AcceptInvitationResultDto? Data = null,
    string? Message = null);

public interface IInvitationService
{
    Task<CreateInvitationOutcome> CreateAsync(long workspaceId, CreateInvitationRequest request, long currentUserId);

    /// <summary>招待の有効性を確認し表示用情報を返す。無効なら null(存在を開示しない)。</summary>
    Task<InvitationPreviewDto?> PreviewAsync(string token);

    Task<AcceptInvitationOutcome> AcceptAsync(string token, AcceptInvitationRequest request);
}
