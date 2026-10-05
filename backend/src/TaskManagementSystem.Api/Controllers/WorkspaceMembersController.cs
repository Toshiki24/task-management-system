using Microsoft.AspNetCore.Mvc;
using TaskManagementSystem.Api.Dtos.Common;
using TaskManagementSystem.Api.Dtos.Workspaces;
using TaskManagementSystem.Api.Extensions;
using TaskManagementSystem.Api.Services;

namespace TaskManagementSystem.Api.Controllers;

[ApiController]
[Route("api/workspaces/{workspaceId}/members")]
public class WorkspaceMembersController : ControllerBase
{
    private readonly IWorkspaceMemberService _memberService;

    public WorkspaceMembersController(IWorkspaceMemberService memberService)
    {
        _memberService = memberService;
    }

    [HttpGet]
    public async Task<ActionResult<List<WorkspaceMemberDto>>> GetAll(long workspaceId)
    {
        var members = await _memberService.GetMembersAsync(workspaceId, this.GetCurrentUserId());
        if (members is null)
        {
            return NotFound(new ErrorResponse("指定されたワークスペースが存在しません。"));
        }

        return Ok(members);
    }

    [HttpPost]
    public async Task<ActionResult<WorkspaceMemberAddedDto>> Add(
        long workspaceId, AddWorkspaceMemberRequest request)
    {
        var outcome = await _memberService.AddMemberAsync(workspaceId, request, this.GetCurrentUserId());

        return outcome.Result switch
        {
            AddWorkspaceMemberResult.WorkspaceNotFound =>
                NotFound(new ErrorResponse("指定されたワークスペースが存在しません。")),

            AddWorkspaceMemberResult.Forbidden => this.ForbiddenError(),

            AddWorkspaceMemberResult.UserNotFound =>
                BadRequest(new ValidationErrorResponse(
                    "入力内容に誤りがあります。",
                    new[] { new ValidationErrorItem("userId", "指定されたユーザーが存在しません。") })),

            AddWorkspaceMemberResult.AlreadyMember =>
                Conflict(new ErrorResponse("指定されたユーザーは既にワークスペースに参加しています。")),

            _ => CreatedAtAction(nameof(GetAll), new { workspaceId }, outcome.Data),
        };
    }

    [HttpPatch("{userId}")]
    public async Task<IActionResult> UpdateRole(
        long workspaceId, long userId, UpdateWorkspaceMemberRoleRequest request)
    {
        var result = await _memberService.UpdateRoleAsync(workspaceId, userId, request, this.GetCurrentUserId());

        return result switch
        {
            UpdateWorkspaceMemberResult.WorkspaceNotFound =>
                NotFound(new ErrorResponse("指定されたワークスペースが存在しません。")),

            UpdateWorkspaceMemberResult.Forbidden => this.ForbiddenError(),

            UpdateWorkspaceMemberResult.MemberNotFound =>
                NotFound(new ErrorResponse("指定されたメンバーが存在しません。")),

            UpdateWorkspaceMemberResult.LastAdmin =>
                Conflict(new ErrorResponse("ワークスペースには少なくとも1人のADMINが必要です。")),

            _ => NoContent(),
        };
    }

    [HttpDelete("{userId}")]
    public async Task<IActionResult> Remove(long workspaceId, long userId)
    {
        var result = await _memberService.RemoveMemberAsync(workspaceId, userId, this.GetCurrentUserId());

        return result switch
        {
            RemoveWorkspaceMemberResult.WorkspaceNotFound =>
                NotFound(new ErrorResponse("指定されたワークスペースが存在しません。")),

            RemoveWorkspaceMemberResult.Forbidden => this.ForbiddenError(),

            RemoveWorkspaceMemberResult.MemberNotFound =>
                NotFound(new ErrorResponse("指定されたメンバーが存在しません。")),

            RemoveWorkspaceMemberResult.LastAdmin =>
                Conflict(new ErrorResponse("ワークスペースには少なくとも1人のADMINが必要です。")),

            _ => NoContent(),
        };
    }
}
