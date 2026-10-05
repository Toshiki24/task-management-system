using Microsoft.AspNetCore.Mvc;
using TaskManagementSystem.Api.Dtos.Common;
using TaskManagementSystem.Api.Dtos.Projects;
using TaskManagementSystem.Api.Extensions;
using TaskManagementSystem.Api.Services;

namespace TaskManagementSystem.Api.Controllers;

/// <summary>
/// ワークスペース配下のプロジェクト一覧・作成(Phase 2 M1 §6)。
/// 詳細・更新・削除は /api/projects/{id}(ProjectsController)。
/// </summary>
[ApiController]
[Route("api/workspaces/{workspaceId}/projects")]
public class WorkspaceProjectsController : ControllerBase
{
    private readonly IProjectService _projectService;

    public WorkspaceProjectsController(IProjectService projectService)
    {
        _projectService = projectService;
    }

    [HttpGet]
    public async Task<ActionResult<List<ProjectDto>>> GetAll(long workspaceId)
    {
        var projects = await _projectService.GetByWorkspaceAsync(workspaceId, this.GetCurrentUserId());
        if (projects is null)
        {
            return NotFound(new ErrorResponse("指定されたワークスペースが存在しません。"));
        }

        return Ok(projects);
    }

    [HttpPost]
    public async Task<ActionResult<ProjectDto>> Create(long workspaceId, ProjectRequest request)
    {
        var outcome = await _projectService.CreateAsync(workspaceId, request, this.GetCurrentUserId());

        return outcome.Result switch
        {
            CreateProjectResult.WorkspaceNotFound =>
                NotFound(new ErrorResponse("指定されたワークスペースが存在しません。")),

            CreateProjectResult.Forbidden => this.ForbiddenError(),

            _ => Created($"/api/projects/{outcome.Data!.Id}", outcome.Data),
        };
    }
}
