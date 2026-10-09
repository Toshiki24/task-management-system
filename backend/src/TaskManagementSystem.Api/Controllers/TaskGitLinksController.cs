using Microsoft.AspNetCore.Mvc;
using TaskManagementSystem.Api.Dtos.Common;
using TaskManagementSystem.Api.Dtos.Git;
using TaskManagementSystem.Api.Extensions;
using TaskManagementSystem.Api.Services;

namespace TaskManagementSystem.Api.Controllers;

[ApiController]
[Route("api/tasks/{taskId}/git/links")]
public class TaskGitLinksController : ControllerBase
{
    private readonly IGitLinkService _service;

    public TaskGitLinksController(IGitLinkService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<ActionResult<List<TaskGitLinkDto>>> GetAll(long taskId)
    {
        var links = await _service.GetByTaskAsync(taskId, this.GetCurrentUserId());
        if (links is null)
        {
            return NotFound(new ErrorResponse("指定されたタスクが存在しません。"));
        }

        return Ok(links);
    }
}
