using TaskManagementSystem.Api.Dtos.ProjectMembers;

namespace TaskManagementSystem.Api.Services;

public enum AddMemberResult
{
    Success,
    ProjectNotFound,
    UserNotFound,
    AlreadyMember,
}

public enum RemoveMemberResult
{
    Success,
    ProjectNotFound,
    MemberNotFound,
}

public record AddMemberOutcome(AddMemberResult Result, MemberAddedDto? Data = null);

public interface IProjectMemberService
{
    Task<List<MemberDto>?> GetMembersAsync(long projectId);
    Task<AddMemberOutcome> AddMemberAsync(long projectId, AddMemberRequest request);
    Task<RemoveMemberResult> RemoveMemberAsync(long projectId, long userId);
}
