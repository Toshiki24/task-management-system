using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaskManagementSystem.Api.Dtos.Auth;
using TaskManagementSystem.Api.Dtos.Common;
using TaskManagementSystem.Api.Services;

namespace TaskManagementSystem.Api.Controllers;

[ApiController]
[Route("api/auth")]
[AllowAnonymous]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService)
    {
        _authService = authService;
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

            default:
                return Ok(outcome.Data);
        }
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
}
