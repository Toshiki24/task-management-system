using Microsoft.AspNetCore.Mvc;
using TaskManagementSystem.Api.Dtos.Common;
using TaskManagementSystem.Api.Dtos.Milestones;
using TaskManagementSystem.Api.Extensions;
using TaskManagementSystem.Api.Services;

namespace TaskManagementSystem.Api.Controllers;

[ApiController]
[Route("api/projects/{projectId}/milestones")]
public class ProjectMilestonesController : ControllerBase
{
    private readonly IMilestoneService _service;

    public ProjectMilestonesController(IMilestoneService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<ActionResult<List<MilestoneDto>>> GetAll(long projectId)
    {
        var milestones = await _service.GetByProjectAsync(projectId, this.GetCurrentUserId());
        if (milestones is null)
        {
            return NotFound(new ErrorResponse("指定されたプロジェクトが存在しません。"));
        }

        return Ok(milestones);
    }

    [HttpPost]
    public async Task<ActionResult<MilestoneDto>> Create(long projectId, MilestoneRequest request)
    {
        var outcome = await _service.CreateAsync(projectId, request, this.GetCurrentUserId());

        return outcome.Result switch
        {
            MilestoneResult.ProjectNotFound =>
                NotFound(new ErrorResponse("指定されたプロジェクトが存在しません。")),
            MilestoneResult.Forbidden => this.ForbiddenError(),
            _ => Created($"/api/milestones/{outcome.Data!.Id}", outcome.Data),
        };
    }
}
