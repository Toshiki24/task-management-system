using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaskManagementSystem.Api.Dtos.Common;
using TaskManagementSystem.Api.Dtos.Invitations;
using TaskManagementSystem.Api.Services;

namespace TaskManagementSystem.Api.Controllers;

/// <summary>
/// トークンによる招待の確認・受諾(Phase 2 M1 §4)。
/// 新規ユーザーもアカウント作成の前にアクセスするため、認証不要(トークンが本人確認を兼ねる)。
/// </summary>
[ApiController]
[Route("api/invitations")]
[AllowAnonymous]
public class InvitationsController : ControllerBase
{
    private readonly IInvitationService _invitationService;

    public InvitationsController(IInvitationService invitationService)
    {
        _invitationService = invitationService;
    }

    /// <summary>招待リンクの有効性確認(表示用)。無効なら404(存在を開示しない)。</summary>
    [HttpGet("{token}")]
    public async Task<ActionResult<InvitationPreviewDto>> Preview(string token)
    {
        var preview = await _invitationService.PreviewAsync(token);
        if (preview is null)
        {
            return NotFound(new ErrorResponse("招待が無効か、有効期限が切れています。"));
        }

        return Ok(preview);
    }

    /// <summary>招待の受諾。既存アカウントが無い場合は名前・パスワードで新規作成する。</summary>
    [HttpPost("{token}/accept")]
    public async Task<ActionResult<AcceptInvitationResultDto>> Accept(string token, AcceptInvitationRequest request)
    {
        var outcome = await _invitationService.AcceptAsync(token, request);

        return outcome.Result switch
        {
            AcceptInvitationResult.Invalid =>
                NotFound(new ErrorResponse("招待が無効か、有効期限が切れています。")),

            AcceptInvitationResult.RegistrationRequired =>
                BadRequest(new ValidationErrorResponse(
                    "入力内容に誤りがあります。",
                    new[] { new ValidationErrorItem("password", outcome.Message ?? "名前とパスワードが必要です。") })),

            _ => Ok(outcome.Data),
        };
    }
}
