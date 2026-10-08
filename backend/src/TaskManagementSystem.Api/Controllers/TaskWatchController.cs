using Microsoft.AspNetCore.Mvc;
using TaskManagementSystem.Api.Dtos.Common;
using TaskManagementSystem.Api.Dtos.Tasks;
using TaskManagementSystem.Api.Extensions;
using TaskManagementSystem.Api.Services;

namespace TaskManagementSystem.Api.Controllers;

[ApiController]
[Route("api/tasks/{taskId}")]
public class TaskWatchController : ControllerBase
{
    private readonly IWatcherService _service;

    public TaskWatchController(IWatcherService service)
    {
        _service = service;
    }

    [HttpGet("watchers")]
    public async Task<ActionResult<WatchersDto>> GetWatchers(long taskId)
    {
        var watchers = await _service.GetWatchersAsync(taskId, this.GetCurrentUserId());
        if (watchers is null)
        {
            return NotFound(new ErrorResponse("指定されたタスクが存在しません。"));
        }

        return Ok(watchers);
    }

    [HttpPost("watch")]
    public async Task<ActionResult<WatchersDto>> Watch(long taskId)
    {
        var result = await _service.WatchAsync(taskId, this.GetCurrentUserId());
        if (result == WatchResult.TaskNotFound)
        {
            return NotFound(new ErrorResponse("指定されたタスクが存在しません。"));
        }

        return Ok(await _service.GetWatchersAsync(taskId, this.GetCurrentUserId()));
    }

    [HttpDelete("watch")]
    public async Task<ActionResult<WatchersDto>> Unwatch(long taskId)
    {
        var result = await _service.UnwatchAsync(taskId, this.GetCurrentUserId());
        if (result == WatchResult.TaskNotFound)
        {
            return NotFound(new ErrorResponse("指定されたタスクが存在しません。"));
        }

        return Ok(await _service.GetWatchersAsync(taskId, this.GetCurrentUserId()));
    }
}
