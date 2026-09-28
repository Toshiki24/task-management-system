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

    public async Task<List<MemberDto>?> GetMembersAsync(long projectId, long currentUserId)
    {
        if (await _dbContext.GetProjectRoleAsync(projectId, currentUserId) is null)
        {
            return null;
        }

        return await _dbContext.ProjectMembers
            .Where(pm => pm.ProjectId == projectId)
            .OrderBy(pm => pm.Id)
            .Select(pm => new MemberDto(pm.UserId, pm.User.Name, pm.User.Email, pm.Role))
            .ToListAsync();
    }

    public async Task<AddMemberOutcome> AddMemberAsync(long projectId, AddMemberRequest request, long currentUserId)
    {
        var role = await _dbContext.GetProjectRoleAsync(projectId, currentUserId);
        if (role is null)
        {
            return new AddMemberOutcome(AddMemberResult.ProjectNotFound);
        }

        if (role != ProjectMemberRole.Owner)
        {
            return new AddMemberOutcome(AddMemberResult.Forbidden);
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

    public async Task<RemoveMemberResult> RemoveMemberAsync(long projectId, long userId, long currentUserId)
    {
        var role = await _dbContext.GetProjectRoleAsync(projectId, currentUserId);
        if (role is null)
        {
            return RemoveMemberResult.ProjectNotFound;
        }

        if (role != ProjectMemberRole.Owner)
        {
            return RemoveMemberResult.Forbidden;
        }

        var member = await _dbContext.ProjectMembers
            .SingleOrDefaultAsync(pm => pm.ProjectId == projectId && pm.UserId == userId);
        if (member is null)
        {
            return RemoveMemberResult.MemberNotFound;
        }

        // OWNERが1人もいなくなると、誰もプロジェクトを管理できなくなるため削除させない(security-review.md SEC-12)
        if (member.Role == ProjectMemberRole.Owner
            && await _dbContext.ProjectMembers.CountAsync(pm => pm.ProjectId == projectId && pm.Role == ProjectMemberRole.Owner) <= 1)
        {
            return RemoveMemberResult.LastOwner;
        }

        // 担当者はプロジェクトメンバーに限るため、削除するユーザーが担当していたタスクは未割り当てにする(SEC-13)。
        // メンバー削除と同じ SaveChanges で保存し、一方だけが反映される状態を防ぐ
        var assignedTasks = await _dbContext.Tasks
            .Where(t => t.ProjectId == projectId && t.AssigneeId == userId)
            .ToListAsync();
        foreach (var task in assignedTasks)
        {
            task.AssigneeId = null;
        }

        _dbContext.ProjectMembers.Remove(member);
        await _dbContext.SaveChangesAsync();

        return RemoveMemberResult.Success;
    }
}
