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

    [HttpPost("login")]
    public async Task<ActionResult<LoginResponse>> Login(LoginRequest request)
    {
        var outcome = await _authService.LoginAsync(request);

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
}
