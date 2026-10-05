using Microsoft.AspNetCore.Mvc;
using TaskManagementSystem.Api.Dtos.Common;
using TaskManagementSystem.Api.Dtos.Projects;
using TaskManagementSystem.Api.Extensions;
using TaskManagementSystem.Api.Services;

namespace TaskManagementSystem.Api.Controllers;

[ApiController]
[Route("api/projects")]
public class ProjectsController : ControllerBase
{
    private readonly IProjectService _projectService;

    public ProjectsController(IProjectService projectService)
    {
        _projectService = projectService;
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<ProjectDto>> GetById(long id)
    {
        var project = await _projectService.GetByIdAsync(id, this.GetCurrentUserId());
        if (project is null)
        {
            return NotFound(new ErrorResponse("指定されたプロジェクトが存在しません。"));
        }

        return Ok(project);
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<ProjectDto>> Update(long id, ProjectRequest request)
    {
        var outcome = await _projectService.UpdateAsync(id, request, this.GetCurrentUserId());

        return outcome.Result switch
        {
            UpdateProjectResult.ProjectNotFound =>
                NotFound(new ErrorResponse("指定されたプロジェクトが存在しません。")),

            UpdateProjectResult.Forbidden => this.ForbiddenError(),

            _ => Ok(outcome.Data),
        };
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(long id)
    {
        var result = await _projectService.DeleteAsync(id, this.GetCurrentUserId());

        return result switch
        {
            DeleteProjectResult.ProjectNotFound =>
                NotFound(new ErrorResponse("指定されたプロジェクトが存在しません。")),

            DeleteProjectResult.Forbidden => this.ForbiddenError(),

            _ => NoContent(),
        };
    }
}
