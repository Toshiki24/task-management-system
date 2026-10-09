using Microsoft.AspNetCore.Mvc;
using TaskManagementSystem.Api.Dtos.Common;
using TaskManagementSystem.Api.Extensions;
using TaskManagementSystem.Api.Services;

namespace TaskManagementSystem.Api.Controllers;

[ApiController]
[Route("api/projects/{projectId}/tasks/export.csv")]
public class ProjectTaskExportController : ControllerBase
{
    private readonly ITaskExportService _service;

    public ProjectTaskExportController(ITaskExportService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<IActionResult> Export(long projectId)
    {
        var csv = await _service.ExportProjectTasksCsvAsync(projectId, this.GetCurrentUserId());
        if (csv is null)
        {
            return NotFound(new ErrorResponse("指定されたプロジェクトが存在しません。"));
        }

        return File(csv, "text/csv; charset=utf-8", $"tasks-{projectId}.csv");
    }
}
