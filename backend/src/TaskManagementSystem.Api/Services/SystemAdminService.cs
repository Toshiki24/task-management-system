using Microsoft.EntityFrameworkCore;
using TaskManagementSystem.Api.Data;
using TaskManagementSystem.Api.Dtos.System;

namespace TaskManagementSystem.Api.Services;

public class SystemAdminService : ISystemAdminService
{
    // 監査ログ取得の最大件数(過大な取得を防ぐ)
    public const int MaxAuditLogLimit = 200;

    private readonly AppDbContext _dbContext;

    public SystemAdminService(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<List<SystemAdminDto>?> GetAdminsAsync(long currentUserId)
    {
        if (!await _dbContext.IsSystemAdminAsync(currentUserId))
        {
            return null;
        }

        return await _dbContext.Users
            .Where(u => u.IsSystemAdmin)
            .OrderBy(u => u.Id)
            .Select(u => new SystemAdminDto(u.Id, u.Name, u.Email))
            .ToListAsync();
    }

    public async Task<GrantSystemAdminResult> GrantAsync(long targetUserId, long currentUserId)
    {
        if (!await _dbContext.IsSystemAdminAsync(currentUserId))
        {
            return GrantSystemAdminResult.Forbidden;
        }

        var target = await _dbContext.Users.SingleOrDefaultAsync(u => u.Id == targetUserId);
        if (target is null)
        {
            return GrantSystemAdminResult.UserNotFound;
        }

        if (target.IsSystemAdmin)
        {
            return GrantSystemAdminResult.AlreadyAdmin;
        }

        target.IsSystemAdmin = true;
        await _dbContext.SaveChangesAsync();

        await _dbContext.RecordAuditAsync(
            currentUserId, AuditActions.SystemAdminGranted, AuditTargets.User, targetUserId);

        return GrantSystemAdminResult.Success;
    }

    public async Task<RevokeSystemAdminResult> RevokeAsync(long targetUserId, long currentUserId)
    {
        if (!await _dbContext.IsSystemAdminAsync(currentUserId))
        {
            return RevokeSystemAdminResult.Forbidden;
        }

        var target = await _dbContext.Users.SingleOrDefaultAsync(u => u.Id == targetUserId);
        if (target is null)
        {
            return RevokeSystemAdminResult.UserNotFound;
        }

        if (!target.IsSystemAdmin)
        {
            return RevokeSystemAdminResult.NotAdmin;
        }

        // 最後の System Admin を剥奪すると誰もインスタンスを管理できなくなるため防ぐ
        if (await _dbContext.Users.CountAsync(u => u.IsSystemAdmin) <= 1)
        {
            return RevokeSystemAdminResult.LastAdmin;
        }

        target.IsSystemAdmin = false;
        await _dbContext.SaveChangesAsync();

        await _dbContext.RecordAuditAsync(
            currentUserId, AuditActions.SystemAdminRevoked, AuditTargets.User, targetUserId);

        return RevokeSystemAdminResult.Success;
    }

    public async Task<List<AuditLogDto>?> GetAuditLogsAsync(long currentUserId, int limit)
    {
        if (!await _dbContext.IsSystemAdminAsync(currentUserId))
        {
            return null;
        }

        var take = Math.Clamp(limit, 1, MaxAuditLogLimit);

        return await _dbContext.AuditLogs
            .OrderByDescending(a => a.Id)
            .Take(take)
            .Select(a => new AuditLogDto(
                a.Id,
                a.ActorUserId,
                a.ActorUser.Name,
                a.Action,
                a.TargetType,
                a.TargetId,
                a.WorkspaceId,
                a.Metadata,
                a.CreatedAt))
            .ToListAsync();
    }
}
