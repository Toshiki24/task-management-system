using Microsoft.AspNetCore.Mvc;
using TaskManagementSystem.Api.Dtos.Auth;
using TaskManagementSystem.Api.Dtos.Common;
using TaskManagementSystem.Api.Extensions;
using TaskManagementSystem.Api.Services.Mfa;

namespace TaskManagementSystem.Api.Controllers;

/// <summary>
/// 本人の MFA(TOTP)設定(M5 §6)。有効化は開始→確認の2段階、無効化・リカバリ再発行は本人のみ。
/// すべて認証必須(ログイン済みユーザーが自分自身の MFA を操作する)。
/// </summary>
[ApiController]
[Route("api/mfa")]
public class MfaController : ControllerBase
{
    private readonly IMfaService _mfaService;

    public MfaController(IMfaService mfaService)
    {
        _mfaService = mfaService;
    }

    [HttpGet]
    public async Task<ActionResult<MfaStatusDto>> GetStatus()
    {
        return Ok(await _mfaService.GetStatusAsync(this.GetCurrentUserId()));
    }

    /// <summary>有効化を開始する(シークレット・otpauth URI を返す)。</summary>
    [HttpPost("enroll")]
    public async Task<ActionResult<MfaEnrollmentDto>> Enroll()
    {
        var outcome = await _mfaService.BeginEnrollmentAsync(this.GetCurrentUserId());
        return outcome.Result switch
        {
            MfaBeginResult.AlreadyEnabled =>
                Conflict(new ErrorResponse("MFA は既に有効です。")),
            _ => Ok(outcome.Data),
        };
    }

    /// <summary>初回コードを検証して有効化し、リカバリコードを発行する。</summary>
    [HttpPost("confirm")]
    public async Task<ActionResult<MfaRecoveryCodesDto>> Confirm(MfaCodeRequest request)
    {
        var outcome = await _mfaService.ConfirmEnrollmentAsync(this.GetCurrentUserId(), request.Code);
        return outcome.Result switch
        {
            MfaConfirmResult.NotStarted =>
                Conflict(new ErrorResponse("先に MFA の有効化を開始してください。")),
            MfaConfirmResult.AlreadyEnabled =>
                Conflict(new ErrorResponse("MFA は既に有効です。")),
            MfaConfirmResult.InvalidCode =>
                BadRequest(new ErrorResponse("コードが正しくありません。認証アプリの時刻同期を確認してください。")),
            _ => Ok(outcome.Data),
        };
    }

    /// <summary>MFA を無効化する(本人のコード入力が必要)。</summary>
    [HttpPost("disable")]
    public async Task<IActionResult> Disable(MfaCodeRequest request)
    {
        var result = await _mfaService.DisableAsync(this.GetCurrentUserId(), request.Code);
        return result switch
        {
            MfaDisableResult.NotEnabled =>
                Conflict(new ErrorResponse("MFA は有効になっていません。")),
            MfaDisableResult.InvalidCode =>
                BadRequest(new ErrorResponse("コードが正しくありません。")),
            _ => NoContent(),
        };
    }

    /// <summary>リカバリコードを再発行する(本人のコード入力が必要)。</summary>
    [HttpPost("recovery-codes")]
    public async Task<ActionResult<MfaRecoveryCodesDto>> RegenerateRecoveryCodes(MfaCodeRequest request)
    {
        var outcome = await _mfaService.RegenerateRecoveryCodesAsync(this.GetCurrentUserId(), request.Code);
        return outcome.Result switch
        {
            MfaRegenerateResult.NotEnabled =>
                Conflict(new ErrorResponse("MFA は有効になっていません。")),
            MfaRegenerateResult.InvalidCode =>
                BadRequest(new ErrorResponse("コードが正しくありません。")),
            _ => Ok(outcome.Data),
        };
    }
}
