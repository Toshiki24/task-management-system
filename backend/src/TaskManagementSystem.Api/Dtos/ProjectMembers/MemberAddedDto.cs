namespace TaskManagementSystem.Api.Dtos.ProjectMembers;

public record MemberAddedDto(long ProjectId, long UserId, string Role);
