using Microsoft.AspNetCore.Mvc;
using TaskManagementSystem.Api.Dtos.Common;
using TaskManagementSystem.Api.Dtos.Git;
using TaskManagementSystem.Api.Extensions;
using TaskManagementSystem.Api.Services;

namespace TaskManagementSystem.Api.Controllers;

[ApiController]
[Route("api/projects/{projectId}/transition-rules")]
public class ProjectTransitionRulesController : ControllerBase
{
    private readonly ITransitionRuleService _service;

    public ProjectTransitionRulesController(ITransitionRuleService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<ActionResult<List<TransitionRuleDto>>> GetAll(long projectId)
    {
        var rules = await _service.GetByProjectAsync(projectId, this.GetCurrentUserId());
        if (rules is null)
        {
            return NotFound(new ErrorResponse("指定されたプロジェクトが存在しません。"));
        }

        return Ok(rules);
    }

    [HttpPut]
    public async Task<ActionResult<List<TransitionRuleDto>>> Replace(long projectId, PutTransitionRulesRequest request)
    {
        var outcome = await _service.ReplaceProjectAsync(projectId, request, this.GetCurrentUserId());

        return outcome.Result switch
        {
            TransitionRuleResult.NotFound =>
                NotFound(new ErrorResponse("指定されたプロジェクトが存在しません。")),
            TransitionRuleResult.Forbidden => this.ForbiddenError(),
            TransitionRuleResult.InvalidStatus =>
                BadRequest(new ValidationErrorResponse(
                    "入力内容に誤りがあります。",
                    new[] { new ValidationErrorItem("toStatusKey", "遷移先の状態が不正です。") })),
            _ => Ok(outcome.Data),
        };
    }
}
