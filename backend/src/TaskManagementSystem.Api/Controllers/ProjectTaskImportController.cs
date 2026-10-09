using Microsoft.AspNetCore.Mvc;
using TaskManagementSystem.Api.Dtos.Common;
using TaskManagementSystem.Api.Dtos.Metrics;
using TaskManagementSystem.Api.Extensions;
using TaskManagementSystem.Api.Services;

namespace TaskManagementSystem.Api.Controllers;

[ApiController]
[Route("api/projects/{projectId}/tasks/import")]
public class ProjectTaskImportController : ControllerBase
{
    private readonly ITaskImportService _service;

    public ProjectTaskImportController(ITaskImportService service)
    {
        _service = service;
    }

    [HttpPost]
    public async Task<ActionResult<ImportResultDto>> Import(long projectId)
    {
        // CSV は生のリクエストボディで受け取る(text/csv)
        string csv;
        using (var reader = new StreamReader(Request.Body))
        {
            csv = await reader.ReadToEndAsync();
        }

        var outcome = await _service.ImportProjectTasksAsync(projectId, csv, this.GetCurrentUserId());

        return outcome.Result switch
        {
            TaskImportResult.ProjectNotFound =>
                NotFound(new ErrorResponse("指定されたプロジェクトが存在しません。")),
            TaskImportResult.Forbidden => this.ForbiddenError(),
            TaskImportResult.TooLarge =>
                BadRequest(new ErrorResponse("一度に取り込める行数の上限を超えています。")),
            _ => Ok(outcome.Data),
        };
    }
}
