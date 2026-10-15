using Microsoft.AspNetCore.Mvc;
using TaskManagementSystem.Api.Dtos.Common;
using TaskManagementSystem.Api.Dtos.Users;
using TaskManagementSystem.Api.Extensions;
using TaskManagementSystem.Api.Services;

namespace TaskManagementSystem.Api.Controllers;

[ApiController]
[Route("api/users")]
public class UsersController : ControllerBase
{
    private readonly IUserService _userService;

    public UsersController(IUserService userService)
    {
        _userService = userService;
    }

    [HttpGet]
    public async Task<ActionResult<List<UserDto>>> GetAll()
    {
        return Ok(await _userService.GetAllAsync());
    }

    /// <summary>本人のメール通知設定を取得する(Phase 2 M5 §8)。</summary>
    [HttpGet("me/notification-settings")]
    public async Task<ActionResult<NotificationSettingsDto>> GetNotificationSettings()
    {
        var settings = await _userService.GetNotificationSettingsAsync(this.GetCurrentUserId());
        if (settings is null)
        {
            return NotFound(new ErrorResponse("指定されたユーザーが存在しません。"));
        }

        return Ok(settings);
    }

    /// <summary>本人のメール通知設定を更新する(Phase 2 M5 §8)。</summary>
    [HttpPut("me/notification-settings")]
    public async Task<ActionResult<NotificationSettingsDto>> UpdateNotificationSettings(
        UpdateNotificationSettingsRequest request)
    {
        var updated = await _userService.UpdateNotificationSettingsAsync(
            this.GetCurrentUserId(), request.EmailEnabled);
        if (!updated)
        {
            return NotFound(new ErrorResponse("指定されたユーザーが存在しません。"));
        }

        return Ok(new NotificationSettingsDto(request.EmailEnabled));
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<UserDto>> GetById(long id)
    {
        var user = await _userService.GetByIdAsync(id);
        if (user is null)
        {
            return NotFound(new ErrorResponse("指定されたユーザーが存在しません。"));
        }

        return Ok(user);
    }
}
