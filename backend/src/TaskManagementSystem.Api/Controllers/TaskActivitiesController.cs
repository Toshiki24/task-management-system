using Microsoft.AspNetCore.Mvc;
using TaskManagementSystem.Api.Dtos.Common;
using TaskManagementSystem.Api.Dtos.Tasks;
using TaskManagementSystem.Api.Extensions;
using TaskManagementSystem.Api.Services;

namespace TaskManagementSystem.Api.Controllers;

[ApiController]
[Route("api/tasks/{taskId}/activities")]
public class TaskActivitiesController : ControllerBase
{
    private readonly IActivityService _service;

    public TaskActivitiesController(IActivityService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<ActionResult<List<ActivityDto>>> GetAll(long taskId)
    {
        var activities = await _service.GetByTaskAsync(taskId, this.GetCurrentUserId());
        if (activities is null)
        {
            return NotFound(new ErrorResponse("指定されたタスクが存在しません。"));
        }

        return Ok(activities);
    }
}
