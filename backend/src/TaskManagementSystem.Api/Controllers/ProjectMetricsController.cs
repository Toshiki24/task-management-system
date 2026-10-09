using Microsoft.AspNetCore.Mvc;
using TaskManagementSystem.Api.Dtos.Common;
using TaskManagementSystem.Api.Dtos.Metrics;
using TaskManagementSystem.Api.Extensions;
using TaskManagementSystem.Api.Services;

namespace TaskManagementSystem.Api.Controllers;

[ApiController]
[Route("api/projects/{projectId}/metrics")]
public class ProjectMetricsController : ControllerBase
{
    private readonly IMetricsService _service;

    public ProjectMetricsController(IMetricsService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<ActionResult<MetricsDto>> Get(long projectId)
    {
        var metrics = await _service.GetProjectMetricsAsync(projectId, this.GetCurrentUserId());
        if (metrics is null)
        {
            return NotFound(new ErrorResponse("指定されたプロジェクトが存在しません。"));
        }

        return Ok(metrics);
    }

    [HttpGet("dev")]
    public async Task<ActionResult<DevMetricsDto>> GetDev(long projectId, [FromQuery] int days = 30)
    {
        var metrics = await _service.GetProjectDevMetricsAsync(projectId, this.GetCurrentUserId(), days);
        if (metrics is null)
        {
            return NotFound(new ErrorResponse("指定されたプロジェクトが存在しません。"));
        }

        return Ok(metrics);
    }
}
