using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using TaskManagementSystem.Api.Dtos.Common;

namespace TaskManagementSystem.Api.Extensions;

public static class ControllerBaseExtensions
{
    public static long GetCurrentUserId(this ControllerBase controller) =>
        long.Parse(controller.User.FindFirstValue(JwtRegisteredClaimNames.Sub)!);

    /// <summary>プロジェクトには所属しているが、操作に必要な権限(OWNER)がない場合の403レスポンス</summary>
    public static ObjectResult ForbiddenError(this ControllerBase controller) =>
        controller.StatusCode(
            StatusCodes.Status403Forbidden,
            new ErrorResponse("この操作を行う権限がありません。"));
}
