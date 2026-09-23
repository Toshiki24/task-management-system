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

    [HttpGet]
    public async Task<ActionResult<List<ProjectDto>>> GetAll()
    {
        return Ok(await _projectService.GetAllAsync());
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<ProjectDto>> GetById(long id)
    {
        var project = await _projectService.GetByIdAsync(id);
        if (project is null)
        {
            return NotFound(new ErrorResponse("指定されたプロジェクトが存在しません。"));
        }

        return Ok(project);
    }

    [HttpPost]
    public async Task<ActionResult<ProjectDto>> Create(ProjectRequest request)
    {
        var created = await _projectService.CreateAsync(request, this.GetCurrentUserId());
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<ProjectDto>> Update(long id, ProjectRequest request)
    {
        var updated = await _projectService.UpdateAsync(id, request);
        if (updated is null)
        {
            return NotFound(new ErrorResponse("指定されたプロジェクトが存在しません。"));
        }

        return Ok(updated);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(long id)
    {
        var deleted = await _projectService.DeleteAsync(id);
        if (!deleted)
        {
            return NotFound(new ErrorResponse("指定されたプロジェクトが存在しません。"));
        }

        return NoContent();
    }
}
