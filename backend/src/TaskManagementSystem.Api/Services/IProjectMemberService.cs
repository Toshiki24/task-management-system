using TaskManagementSystem.Api.Dtos.ProjectMembers;

namespace TaskManagementSystem.Api.Services;

public enum AddMemberResult
{
    Success,
    ProjectNotFound,
    Forbidden,
    UserNotFound,
    AlreadyMember,
}

public enum RemoveMemberResult
{
    Success,
    ProjectNotFound,
    Forbidden,
    MemberNotFound,
    LastOwner,
}

public record AddMemberOutcome(AddMemberResult Result, MemberAddedDto? Data = null);

public interface IProjectMemberService
{
    Task<List<MemberDto>?> GetMembersAsync(long projectId, long currentUserId);
    Task<AddMemberOutcome> AddMemberAsync(long projectId, AddMemberRequest request, long currentUserId);
    Task<RemoveMemberResult> RemoveMemberAsync(long projectId, long userId, long currentUserId);
}
