using Microsoft.AspNetCore.Mvc;
using TaskManagementSystem.Api.Dtos.Common;
using TaskManagementSystem.Api.Dtos.Git;
using TaskManagementSystem.Api.Extensions;
using TaskManagementSystem.Api.Services;

namespace TaskManagementSystem.Api.Controllers;

[ApiController]
[Route("api/workspaces/{workspaceId}/git-identities")]
public class WorkspaceGitIdentitiesController : ControllerBase
{
    private readonly IGitIdentityService _service;

    public WorkspaceGitIdentitiesController(IGitIdentityService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<ActionResult<List<GitIdentityDto>>> GetAll(long workspaceId)
    {
        var identities = await _service.GetByWorkspaceAsync(workspaceId, this.GetCurrentUserId());
        if (identities is null)
        {
            return NotFound(new ErrorResponse("指定されたワークスペースが存在しません。"));
        }

        return Ok(identities);
    }

    [HttpPost]
    public async Task<ActionResult<GitIdentityDto>> Create(long workspaceId, CreateGitIdentityRequest request)
    {
        var outcome = await _service.CreateAsync(workspaceId, request, this.GetCurrentUserId());

        return outcome.Result switch
        {
            GitIdentityResult.WorkspaceNotFound =>
                NotFound(new ErrorResponse("指定されたワークスペースが存在しません。")),
            GitIdentityResult.Forbidden => this.ForbiddenError(),
            GitIdentityResult.UserNotMember =>
                BadRequest(new ValidationErrorResponse(
                    "入力内容に誤りがあります。",
                    new[] { new ValidationErrorItem("userId", "指定されたユーザーはワークスペースのメンバーではありません。") })),
            GitIdentityResult.Duplicate =>
                Conflict(new ErrorResponse("この Git ユーザーは既に対応付けられています。")),
            _ => Created($"/api/git-identities/{outcome.Data!.Id}", outcome.Data),
        };
    }
}
