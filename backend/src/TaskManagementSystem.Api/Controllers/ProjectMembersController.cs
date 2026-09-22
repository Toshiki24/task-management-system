using Microsoft.AspNetCore.Mvc;
using TaskManagementSystem.Api.Dtos.Common;
using TaskManagementSystem.Api.Dtos.ProjectMembers;
using TaskManagementSystem.Api.Services;

namespace TaskManagementSystem.Api.Controllers;

[ApiController]
[Route("api/projects/{projectId}/members")]
public class ProjectMembersController : ControllerBase
{
    private readonly IProjectMemberService _projectMemberService;

    public ProjectMembersController(IProjectMemberService projectMemberService)
    {
        _projectMemberService = projectMemberService;
    }

    [HttpGet]
    public async Task<ActionResult<List<MemberDto>>> GetAll(long projectId)
    {
        var members = await _projectMemberService.GetMembersAsync(projectId);
        if (members is null)
        {
            return NotFound(new ErrorResponse("指定されたプロジェクトが存在しません。"));
        }

        return Ok(members);
    }

    [HttpPost]
    public async Task<ActionResult<MemberAddedDto>> Add(long projectId, AddMemberRequest request)
    {
        var outcome = await _projectMemberService.AddMemberAsync(projectId, request);

        return outcome.Result switch
        {
            AddMemberResult.ProjectNotFound =>
                NotFound(new ErrorResponse("指定されたプロジェクトが存在しません。")),

            AddMemberResult.UserNotFound =>
                BadRequest(new ValidationErrorResponse(
                    "入力内容に誤りがあります。",
                    new[] { new ValidationErrorItem("userId", "指定されたユーザーが存在しません。") })),

            AddMemberResult.AlreadyMember =>
                Conflict(new ErrorResponse("指定されたユーザーは既にプロジェクトに参加しています。")),

            _ => CreatedAtAction(nameof(GetAll), new { projectId }, outcome.Data),
        };
    }

    [HttpDelete("{userId}")]
    public async Task<IActionResult> Remove(long projectId, long userId)
    {
        var result = await _projectMemberService.RemoveMemberAsync(projectId, userId);

        return result switch
        {
            RemoveMemberResult.ProjectNotFound =>
                NotFound(new ErrorResponse("指定されたプロジェクトが存在しません。")),

            RemoveMemberResult.MemberNotFound =>
                NotFound(new ErrorResponse("指定されたメンバーが存在しません。")),

            _ => NoContent(),
        };
    }
}
