using Microsoft.AspNetCore.Mvc;
using TaskManagementSystem.Api.Dtos.Common;
using TaskManagementSystem.Api.Dtos.Git;
using TaskManagementSystem.Api.Extensions;
using TaskManagementSystem.Api.Services;

namespace TaskManagementSystem.Api.Controllers;

[ApiController]
[Route("api/projects/{projectId}/repository-links")]
public class ProjectRepositoryLinksController : ControllerBase
{
    private readonly IRepositoryLinkService _service;

    public ProjectRepositoryLinksController(IRepositoryLinkService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<ActionResult<List<RepositoryLinkDto>>> GetAll(long projectId)
    {
        var links = await _service.GetByProjectAsync(projectId, this.GetCurrentUserId());
        if (links is null)
        {
            return NotFound(new ErrorResponse("指定されたプロジェクトが存在しません。"));
        }

        return Ok(links);
    }

    [HttpPost]
    public async Task<ActionResult<RepositoryLinkDto>> Create(long projectId, CreateRepositoryLinkRequest request)
    {
        var outcome = await _service.CreateAsync(projectId, request, this.GetCurrentUserId());

        return outcome.Result switch
        {
            RepositoryLinkResult.ProjectNotFound =>
                NotFound(new ErrorResponse("指定されたプロジェクトが存在しません。")),
            RepositoryLinkResult.Forbidden => this.ForbiddenError(),
            RepositoryLinkResult.InvalidConnection =>
                BadRequest(new ValidationErrorResponse(
                    "入力内容に誤りがあります。",
                    new[] { new ValidationErrorItem("gitConnectionId", "指定された接続が不正です(同一ワークスペースの接続を指定してください)。") })),
            RepositoryLinkResult.Duplicate =>
                Conflict(new ErrorResponse("このリポジトリは既に連携されています。")),
            _ => Created($"/api/repository-links/{outcome.Data!.Id}", outcome.Data),
        };
    }
}
