using Microsoft.AspNetCore.Mvc;
using TaskManagementSystem.Api.Dtos.Common;
using TaskManagementSystem.Api.Dtos.Invitations;
using TaskManagementSystem.Api.Extensions;
using TaskManagementSystem.Api.Services;

namespace TaskManagementSystem.Api.Controllers;

[ApiController]
[Route("api/workspaces/{workspaceId}/invitations")]
public class WorkspaceInvitationsController : ControllerBase
{
    private readonly IInvitationService _invitationService;

    public WorkspaceInvitationsController(IInvitationService invitationService)
    {
        _invitationService = invitationService;
    }

    /// <summary>ワークスペースへの招待を発行する(対象WSのAdmin / System Admin)。</summary>
    [HttpPost]
    public async Task<ActionResult<InvitationCreatedDto>> Create(
        long workspaceId, CreateInvitationRequest request)
    {
        var outcome = await _invitationService.CreateAsync(workspaceId, request, this.GetCurrentUserId());

        return outcome.Result switch
        {
            CreateInvitationResult.WorkspaceNotFound =>
                NotFound(new ErrorResponse("指定されたワークスペースが存在しません。")),

            CreateInvitationResult.Forbidden => this.ForbiddenError(),

            CreateInvitationResult.AlreadyMember =>
                Conflict(new ErrorResponse("指定されたユーザーは既にワークスペースに参加しています。")),

            _ => Created(string.Empty, outcome.Data),
        };
    }
}
