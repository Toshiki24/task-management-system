using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;

namespace TaskManagementSystem.Api.Extensions;

public static class ControllerBaseExtensions
{
    public static long GetCurrentUserId(this ControllerBase controller) =>
        long.Parse(controller.User.FindFirstValue(JwtRegisteredClaimNames.Sub)!);
}
