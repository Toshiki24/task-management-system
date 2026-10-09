using Microsoft.AspNetCore.Mvc;
using TaskManagementSystem.Api.Dtos.Common;
using TaskManagementSystem.Api.Dtos.Git;
using TaskManagementSystem.Api.Extensions;
using TaskManagementSystem.Api.Services;

namespace TaskManagementSystem.Api.Controllers;

[ApiController]
[Route("api/tasks/{taskId}/git")]
public class TaskGitActionsController : ControllerBase
{
    private readonly IGitActionService _service;

    public TaskGitActionsController(IGitActionService service)
    {
        _service = service;
    }

    [HttpPost("branch")]
    public async Task<ActionResult<TaskGitLinkDto>> CreateBranch(long taskId, CreateBranchRequest request)
    {
        var outcome = await _service.CreateBranchAsync(taskId, request, this.GetCurrentUserId());
        return ToResult(outcome);
    }

    [HttpPost("pull-request")]
    public async Task<ActionResult<TaskGitLinkDto>> CreatePullRequest(long taskId, CreatePullRequestRequest request)
    {
        var outcome = await _service.CreatePullRequestAsync(taskId, request, this.GetCurrentUserId());
        return ToResult(outcome);
    }

    private ActionResult<TaskGitLinkDto> ToResult(GitActionOutcome outcome) => outcome.Result switch
    {
        GitActionResult.TaskNotFound =>
            NotFound(new ErrorResponse("指定されたタスクが存在しません。")),
        GitActionResult.Forbidden => this.ForbiddenError(),
        GitActionResult.InvalidRepositoryLink =>
            BadRequest(new ValidationErrorResponse(
                "入力内容に誤りがあります。",
                new[] { new ValidationErrorItem("repositoryLinkId", "指定された連携リポジトリが不正です。") })),
        GitActionResult.ProviderUnavailable =>
            BadRequest(new ErrorResponse("対応していないプロバイダです。")),
        GitActionResult.ProviderError =>
            StatusCode(502, new ErrorResponse("Git プロバイダでの操作に失敗しました。")),
        _ => Created($"/api/tasks/{outcome.Data!.TaskId}/git/links", outcome.Data),
    };
}
