using Microsoft.AspNetCore.Mvc;
using TaskManagementSystem.Api.Dtos.Common;
using TaskManagementSystem.Api.Dtos.Cycles;
using TaskManagementSystem.Api.Extensions;
using TaskManagementSystem.Api.Services;

namespace TaskManagementSystem.Api.Controllers;

[ApiController]
[Route("api/projects/{projectId}/cycles")]
public class ProjectCyclesController : ControllerBase
{
    private readonly ICycleService _service;

    public ProjectCyclesController(ICycleService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<ActionResult<List<CycleDto>>> GetAll(long projectId)
    {
        var cycles = await _service.GetByProjectAsync(projectId, this.GetCurrentUserId());
        if (cycles is null)
        {
            return NotFound(new ErrorResponse("指定されたプロジェクトが存在しません。"));
        }

        return Ok(cycles);
    }

    [HttpPost]
    public async Task<ActionResult<CycleDto>> Create(long projectId, CycleRequest request)
    {
        var outcome = await _service.CreateAsync(projectId, request, this.GetCurrentUserId());

        return outcome.Result switch
        {
            CycleResult.ProjectNotFound =>
                NotFound(new ErrorResponse("指定されたプロジェクトが存在しません。")),
            CycleResult.Forbidden => this.ForbiddenError(),
            _ => Created($"/api/cycles/{outcome.Data!.Id}", outcome.Data),
        };
    }
}
