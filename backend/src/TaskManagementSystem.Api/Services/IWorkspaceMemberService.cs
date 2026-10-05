using TaskManagementSystem.Api.Dtos.Workspaces;

namespace TaskManagementSystem.Api.Services;

public enum AddWorkspaceMemberResult
{
    Success,
    WorkspaceNotFound,
    Forbidden,
    UserNotFound,
    AlreadyMember,
}

public enum UpdateWorkspaceMemberResult
{
    Success,
    WorkspaceNotFound,
    Forbidden,
    MemberNotFound,
    LastAdmin,
}

public enum RemoveWorkspaceMemberResult
{
    Success,
    WorkspaceNotFound,
    Forbidden,
    MemberNotFound,
    LastAdmin,
}

public record AddWorkspaceMemberOutcome(AddWorkspaceMemberResult Result, WorkspaceMemberAddedDto? Data = null);

public interface IWorkspaceMemberService
{
    /// <summary>メンバー一覧。ワークスペースを閲覧できない場合は null(= 404)。</summary>
    Task<List<WorkspaceMemberDto>?> GetMembersAsync(long workspaceId, long currentUserId);

    Task<AddWorkspaceMemberOutcome> AddMemberAsync(
        long workspaceId, AddWorkspaceMemberRequest request, long currentUserId);

    Task<UpdateWorkspaceMemberResult> UpdateRoleAsync(
        long workspaceId, long userId, UpdateWorkspaceMemberRoleRequest request, long currentUserId);

    Task<RemoveWorkspaceMemberResult> RemoveMemberAsync(long workspaceId, long userId, long currentUserId);
}
