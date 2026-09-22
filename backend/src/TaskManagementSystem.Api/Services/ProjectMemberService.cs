using Microsoft.EntityFrameworkCore;
using TaskManagementSystem.Api.Data;
using TaskManagementSystem.Api.Dtos.ProjectMembers;
using TaskManagementSystem.Api.Models;

namespace TaskManagementSystem.Api.Services;

public class ProjectMemberService : IProjectMemberService
{
    private readonly AppDbContext _dbContext;

    public ProjectMemberService(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<List<MemberDto>?> GetMembersAsync(long projectId)
    {
        var projectExists = await _dbContext.Projects.AnyAsync(p => p.Id == projectId);
        if (!projectExists)
        {
            return null;
        }

        return await _dbContext.ProjectMembers
            .Where(pm => pm.ProjectId == projectId)
            .OrderBy(pm => pm.Id)
            .Select(pm => new MemberDto(pm.UserId, pm.User.Name, pm.User.Email, pm.Role))
            .ToListAsync();
    }

    public async Task<AddMemberOutcome> AddMemberAsync(long projectId, AddMemberRequest request)
    {
        var projectExists = await _dbContext.Projects.AnyAsync(p => p.Id == projectId);
        if (!projectExists)
        {
            return new AddMemberOutcome(AddMemberResult.ProjectNotFound);
        }

        var userExists = await _dbContext.Users.AnyAsync(u => u.Id == request.UserId);
        if (!userExists)
        {
            return new AddMemberOutcome(AddMemberResult.UserNotFound);
        }

        var alreadyMember = await _dbContext.ProjectMembers
            .AnyAsync(pm => pm.ProjectId == projectId && pm.UserId == request.UserId);
        if (alreadyMember)
        {
            return new AddMemberOutcome(AddMemberResult.AlreadyMember);
        }

        var member = new ProjectMember
        {
            ProjectId = projectId,
            UserId = request.UserId!.Value,
            Role = request.Role!,
        };

        _dbContext.ProjectMembers.Add(member);
        await _dbContext.SaveChangesAsync();

        return new AddMemberOutcome(
            AddMemberResult.Success,
            new MemberAddedDto(projectId, member.UserId, member.Role));
    }

    public async Task<RemoveMemberResult> RemoveMemberAsync(long projectId, long userId)
    {
        var projectExists = await _dbContext.Projects.AnyAsync(p => p.Id == projectId);
        if (!projectExists)
        {
            return RemoveMemberResult.ProjectNotFound;
        }

        var member = await _dbContext.ProjectMembers
            .SingleOrDefaultAsync(pm => pm.ProjectId == projectId && pm.UserId == userId);
        if (member is null)
        {
            return RemoveMemberResult.MemberNotFound;
        }

        _dbContext.ProjectMembers.Remove(member);
        await _dbContext.SaveChangesAsync();

        return RemoveMemberResult.Success;
    }
}
