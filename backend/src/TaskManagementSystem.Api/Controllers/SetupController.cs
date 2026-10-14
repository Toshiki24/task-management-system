using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaskManagementSystem.Api.Dtos.Common;
using TaskManagementSystem.Api.Dtos.Setup;
using TaskManagementSystem.Api.Services;

namespace TaskManagementSystem.Api.Controllers;

/// <summary>
/// 初回セットアップ(初回 System Admin 作成。M5 §8)。セルフホスト専用で、ユーザーがまだ居ないため匿名で呼べる。
/// 機能が無効(AWS 本番など <c>SELF_HOST_SETUP</c> 未設定)の場合は 404 を返し、存在しないものとして振る舞う。
/// </summary>
[ApiController]
[Route("api/setup")]
[AllowAnonymous]
public class SetupController : ControllerBase
{
    private readonly ISetupService _setupService;

    public SetupController(ISetupService setupService)
    {
        _setupService = setupService;
    }

    /// <summary>初回セットアップが必要かを返す。機密情報は返さない。</summary>
    [HttpGet("status")]
    public async Task<ActionResult<SetupStatusDto>> GetStatus()
    {
        var outcome = await _setupService.GetStatusAsync();
        return outcome.Result switch
        {
            SetupStatusResult.Disabled => NotFound(new ErrorResponse("指定されたAPIは存在しません。")),
            // DB 障害時は状態不明として 503。未セットアップ(作成可能)とは絶対に誤認させない
            SetupStatusResult.DbError => StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                new ErrorResponse("セットアップ状態を確認できませんでした。時間をおいて再度お試しください。")),
            _ => Ok(outcome.Data),
        };
    }

    /// <summary>初回 System Admin を作成する。ユーザーが1人も居ないときだけ成功する。</summary>
    [HttpPost("admin")]
    public async Task<IActionResult> CreateAdmin(CreateInitialAdminRequest request)
    {
        var result = await _setupService.CreateInitialAdminAsync(request);
        return result switch
        {
            CreateInitialAdminResult.Disabled =>
                NotFound(new ErrorResponse("指定されたAPIは存在しません。")),
            CreateInitialAdminResult.AlreadyInitialized =>
                Conflict(new ErrorResponse("セットアップは既に完了しています。")),
            CreateInitialAdminResult.DbError => StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                new ErrorResponse("管理者を作成できませんでした。時間をおいて再度お試しください。")),
            _ => StatusCode(StatusCodes.Status201Created),
        };
    }
}
