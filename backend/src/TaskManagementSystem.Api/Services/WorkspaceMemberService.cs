using Microsoft.EntityFrameworkCore;
using TaskManagementSystem.Api.Data;
using TaskManagementSystem.Api.Dtos.Workspaces;
using TaskManagementSystem.Api.Models;

namespace TaskManagementSystem.Api.Services;

public class WorkspaceMemberService : IWorkspaceMemberService
{
    private readonly AppDbContext _dbContext;

    public WorkspaceMemberService(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<List<WorkspaceMemberDto>?> GetMembersAsync(long workspaceId, long currentUserId)
    {
        var access = await _dbContext.ResolveWorkspaceAccessAsync(workspaceId, currentUserId);
        if (access is null || !access.Value.CanView)
        {
            return null;
        }

        return await _dbContext.WorkspaceMembers
            .Where(wm => wm.WorkspaceId == workspaceId)
            .OrderBy(wm => wm.Id)
            .Select(wm => new WorkspaceMemberDto(wm.UserId, wm.User.Name, wm.User.Email, wm.Role))
            .ToListAsync();
    }

    public async Task<AddWorkspaceMemberOutcome> AddMemberAsync(
        long workspaceId, AddWorkspaceMemberRequest request, long currentUserId)
    {
        var access = await _dbContext.ResolveWorkspaceAccessAsync(workspaceId, currentUserId);
        if (access is null || !access.Value.CanView)
        {
            return new AddWorkspaceMemberOutcome(AddWorkspaceMemberResult.WorkspaceNotFound);
        }

        if (!access.Value.IsAdmin)
        {
            return new AddWorkspaceMemberOutcome(AddWorkspaceMemberResult.Forbidden);
        }

        var userExists = await _dbContext.Users.AnyAsync(u => u.Id == request.UserId);
        if (!userExists)
        {
            return new AddWorkspaceMemberOutcome(AddWorkspaceMemberResult.UserNotFound);
        }

        var alreadyMember = await _dbContext.WorkspaceMembers
            .AnyAsync(wm => wm.WorkspaceId == workspaceId && wm.UserId == request.UserId);
        if (alreadyMember)
        {
            return new AddWorkspaceMemberOutcome(AddWorkspaceMemberResult.AlreadyMember);
        }

        var member = new WorkspaceMember
        {
            WorkspaceId = workspaceId,
            UserId = request.UserId!.Value,
            Role = request.Role!,
        };

        _dbContext.WorkspaceMembers.Add(member);
        await _dbContext.SaveChangesAsync();

        await _dbContext.RecordAuditAsync(
            currentUserId, AuditActions.MemberAdded, AuditTargets.User, member.UserId, workspaceId,
            new { member.Role });

        return new AddWorkspaceMemberOutcome(
            AddWorkspaceMemberResult.Success,
            new WorkspaceMemberAddedDto(workspaceId, member.UserId, member.Role));
    }

    public async Task<UpdateWorkspaceMemberResult> UpdateRoleAsync(
        long workspaceId, long userId, UpdateWorkspaceMemberRoleRequest request, long currentUserId)
    {
        var access = await _dbContext.ResolveWorkspaceAccessAsync(workspaceId, currentUserId);
        if (access is null || !access.Value.CanView)
        {
            return UpdateWorkspaceMemberResult.WorkspaceNotFound;
        }

        if (!access.Value.IsAdmin)
        {
            return UpdateWorkspaceMemberResult.Forbidden;
        }

        var member = await _dbContext.WorkspaceMembers
            .SingleOrDefaultAsync(wm => wm.WorkspaceId == workspaceId && wm.UserId == userId);
        if (member is null)
        {
            return UpdateWorkspaceMemberResult.MemberNotFound;
        }

        // 最後の ADMIN を降格すると誰もワークスペースを管理できなくなるため防ぐ(ProjectMember の LastOwner と同方針)
        if (member.Role == WorkspaceMemberRole.Admin && request.Role != WorkspaceMemberRole.Admin
            && await LastAdminAsync(workspaceId))
        {
            return UpdateWorkspaceMemberResult.LastAdmin;
        }

        var previousRole = member.Role;
        member.Role = request.Role!;
        await _dbContext.SaveChangesAsync();

        await _dbContext.RecordAuditAsync(
            currentUserId, AuditActions.MemberRoleChanged, AuditTargets.User, userId, workspaceId,
            new { from = previousRole, to = member.Role });

        return UpdateWorkspaceMemberResult.Success;
    }

    public async Task<RemoveWorkspaceMemberResult> RemoveMemberAsync(
        long workspaceId, long userId, long currentUserId)
    {
        var access = await _dbContext.ResolveWorkspaceAccessAsync(workspaceId, currentUserId);
        if (access is null || !access.Value.CanView)
        {
            return RemoveWorkspaceMemberResult.WorkspaceNotFound;
        }

        if (!access.Value.IsAdmin)
        {
            return RemoveWorkspaceMemberResult.Forbidden;
        }

        var member = await _dbContext.WorkspaceMembers
            .SingleOrDefaultAsync(wm => wm.WorkspaceId == workspaceId && wm.UserId == userId);
        if (member is null)
        {
            return RemoveWorkspaceMemberResult.MemberNotFound;
        }

        if (member.Role == WorkspaceMemberRole.Admin && await LastAdminAsync(workspaceId))
        {
            return RemoveWorkspaceMemberResult.LastAdmin;
        }

        _dbContext.WorkspaceMembers.Remove(member);
        await _dbContext.SaveChangesAsync();

        await _dbContext.RecordAuditAsync(
            currentUserId, AuditActions.MemberRemoved, AuditTargets.User, userId, workspaceId);

        return RemoveWorkspaceMemberResult.Success;
    }

    // 対象ワークスペースの ADMIN が 1 人以下か
    private async Task<bool> LastAdminAsync(long workspaceId) =>
        await _dbContext.WorkspaceMembers
            .CountAsync(wm => wm.WorkspaceId == workspaceId && wm.Role == WorkspaceMemberRole.Admin) <= 1;
}
