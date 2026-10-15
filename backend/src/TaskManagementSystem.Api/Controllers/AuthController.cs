using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaskManagementSystem.Api.Dtos.Auth;
using TaskManagementSystem.Api.Dtos.Common;
using TaskManagementSystem.Api.Dtos.Sso;
using TaskManagementSystem.Api.Services;

namespace TaskManagementSystem.Api.Controllers;

[ApiController]
[Route("api/auth")]
[AllowAnonymous]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly ISsoService _ssoService;

    public AuthController(IAuthService authService, ISsoService ssoService)
    {
        _authService = authService;
        _ssoService = ssoService;
    }

    /// <summary>BFFが実クライアントIPを転送するヘッダー。IP単位のログイン制限に使う(security-review-2.md SEC2-03)</summary>
    private const string ClientIpHeaderName = "X-Client-IP";

    [HttpPost("login")]
    public async Task<ActionResult<LoginResponse>> Login(LoginRequest request)
    {
        // API は X-Origin-Verify で BFF からの呼び出しのみを受け付けるため、BFFが転送したIPを信頼できる
        var clientIp = Request.Headers[ClientIpHeaderName].ToString();
        var outcome = await _authService.LoginAsync(request, string.IsNullOrEmpty(clientIp) ? null : clientIp);

        switch (outcome.Result)
        {
            case LoginResult.InvalidCredentials:
                return Unauthorized(new ErrorResponse("メールアドレスまたはパスワードが正しくありません。"));

            case LoginResult.TooManyAttempts:
                // 何秒後に再試行できるかを標準のRetry-Afterヘッダーで返す(秒単位、切り上げ)
                Response.Headers.RetryAfter =
                    Math.Max(1, (int)Math.Ceiling(outcome.RetryAfter!.Value.TotalSeconds)).ToString();
                return StatusCode(
                    StatusCodes.Status429TooManyRequests,
                    new ErrorResponse("ログインの試行回数が上限に達しました。しばらくしてから再度お試しください。"));

            case LoginResult.MfaRequired:
                // パスワード認証のみ成功。セッションはまだ発行せず、MFA 検証ステップへ誘導する
                return Ok(new MfaChallengeResponse(true, outcome.MfaToken!));

            default:
                return Ok(outcome.Data);
        }
    }

    /// <summary>ログインの MFA 検証ステップ。成功すると通常ログインと同じトークンを返す。</summary>
    [HttpPost("mfa/verify")]
    public async Task<ActionResult<LoginResponse>> VerifyMfa(MfaVerifyRequest request)
    {
        var outcome = await _authService.VerifyMfaAsync(request);
        return outcome.Result switch
        {
            MfaVerifyResult.InvalidToken =>
                Unauthorized(new ErrorResponse("認証の有効期限が切れました。もう一度ログインしてください。")),
            MfaVerifyResult.InvalidCode =>
                Unauthorized(new ErrorResponse("コードが正しくありません。")),
            _ => Ok(outcome.Data),
        };
    }

    /// <summary>リフレッシュトークンでアクセストークンを再発行する(リフレッシュトークンも新しいものに置き換わる)</summary>
    [HttpPost("refresh")]
    public async Task<ActionResult<LoginResponse>> Refresh(RefreshTokenRequest request)
    {
        var response = await _authService.RefreshAsync(request);
        if (response is null)
        {
            return Unauthorized(new ErrorResponse("認証の有効期限が切れました。再度ログインしてください。"));
        }

        return Ok(response);
    }

    /// <summary>リフレッシュトークンを失効させる。既に失効している場合も成功として扱う</summary>
    [HttpPost("logout")]
    public async Task<IActionResult> Logout(RefreshTokenRequest request)
    {
        await _authService.LogoutAsync(request);
        return NoContent();
    }

    /// <summary>ログイン画面に出す有効な SSO 接続の一覧(公開)。</summary>
    [HttpGet("sso/connections")]
    public async Task<ActionResult<List<PublicSsoConnectionDto>>> GetSsoConnections() =>
        Ok(await _ssoService.GetEnabledConnectionsAsync());

    /// <summary>SSO 認可を開始し、IdP への遷移先 URL を返す(BFF がブラウザをリダイレクトする)。</summary>
    [HttpGet("sso/{id}/start")]
    public async Task<ActionResult<SsoStartDto>> StartSso(long id, [FromQuery] string redirectUri)
    {
        if (string.IsNullOrWhiteSpace(redirectUri))
        {
            return BadRequest(new ErrorResponse("redirectUri は必須です。"));
        }

        var outcome = await _ssoService.StartAsync(id, redirectUri);
        return outcome.Result switch
        {
            SsoStartResult.NotFound =>
                NotFound(new ErrorResponse("指定された SSO 接続が存在しないか、無効です。")),
            SsoStartResult.Error =>
                StatusCode(StatusCodes.Status502BadGateway,
                    new ErrorResponse("SSO プロバイダに接続できませんでした。")),
            _ => Ok(new SsoStartDto(outcome.AuthorizationUrl!)),
        };
    }

    /// <summary>SSO コールバック。認可コードを検証し、通常ログインと同じトークンを返す。</summary>
    [HttpPost("sso/callback")]
    public async Task<ActionResult<LoginResponse>> SsoCallback(SsoCallbackRequest request)
    {
        var outcome = await _ssoService.CompleteAsync(request);
        return outcome.Result switch
        {
            SsoCallbackResult.InvalidState =>
                Unauthorized(new ErrorResponse("SSO セッションが無効か、期限切れです。もう一度お試しください。")),
            SsoCallbackResult.NotProvisioned =>
                StatusCode(StatusCodes.Status403Forbidden,
                    new ErrorResponse("このアカウントはログインを許可されていません。管理者にお問い合わせください。")),
            SsoCallbackResult.Error =>
                StatusCode(StatusCodes.Status502BadGateway,
                    new ErrorResponse("SSO 認証の検証に失敗しました。")),
            _ => Ok(outcome.Response),
        };
    }
}
