using Microsoft.EntityFrameworkCore;
using TaskManagementSystem.Api.Data;
using TaskManagementSystem.Api.Dtos.System;

namespace TaskManagementSystem.Api.Services;

public class SystemAdminService : ISystemAdminService
{
    // 監査ログ取得の最大件数(過大な取得を防ぐ)
    public const int MaxAuditLogLimit = 200;

    // 「直近のアクティビティ数」の集計期間(日)
    public const int RecentActivityDays = 7;

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

    public async Task<AuditLogPageDto?> GetAuditLogsAsync(long currentUserId, AuditLogQuery query)
    {
        if (!await _dbContext.IsSystemAdminAsync(currentUserId))
        {
            return null;
        }

        var take = Math.Clamp(query.Limit, 1, MaxAuditLogLimit);
        var skip = Math.Max(query.Offset, 0);

        var logs = _dbContext.AuditLogs.AsQueryable();

        if (query.ActorUserId is { } actorUserId)
        {
            logs = logs.Where(a => a.ActorUserId == actorUserId);
        }

        if (!string.IsNullOrWhiteSpace(query.Action))
        {
            logs = logs.Where(a => a.Action == query.Action);
        }

        if (!string.IsNullOrWhiteSpace(query.TargetType))
        {
            logs = logs.Where(a => a.TargetType == query.TargetType);
        }

        if (query.From is { } from)
        {
            var fromValue = DateTime.SpecifyKind(from, DateTimeKind.Unspecified);
            logs = logs.Where(a => a.CreatedAt >= fromValue);
        }

        if (query.To is { } to)
        {
            var toValue = DateTime.SpecifyKind(to, DateTimeKind.Unspecified);
            logs = logs.Where(a => a.CreatedAt <= toValue);
        }

        var total = await logs.CountAsync();

        var items = await logs
            .OrderByDescending(a => a.Id)
            .Skip(skip)
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

        return new AuditLogPageDto(items, total);
    }

    public async Task<SystemStatsDto?> GetStatsAsync(long currentUserId)
    {
        if (!await _dbContext.IsSystemAdminAsync(currentUserId))
        {
            return null;
        }

        // DB は timestamp without time zone のため、比較パラメータは Unspecified にして送る
        var since = DateTime.SpecifyKind(
            DateTime.UtcNow.AddDays(-RecentActivityDays), DateTimeKind.Unspecified);

        return new SystemStatsDto(
            WorkspaceCount: await _dbContext.Workspaces.CountAsync(),
            ProjectCount: await _dbContext.Projects.CountAsync(),
            TaskCount: await _dbContext.Tasks.CountAsync(),
            UserCount: await _dbContext.Users.CountAsync(),
            SystemAdminCount: await _dbContext.Users.CountAsync(u => u.IsSystemAdmin),
            RecentActivityCount: await _dbContext.Activities.CountAsync(a => a.CreatedAt >= since));
    }

    public async Task<List<AdminWorkspaceDto>?> GetWorkspacesAsync(long currentUserId)
    {
        if (!await _dbContext.IsSystemAdminAsync(currentUserId))
        {
            return null;
        }

        return await _dbContext.Workspaces
            .OrderByDescending(w => w.Id)
            .Select(w => new AdminWorkspaceDto(
                w.Id,
                w.Name,
                w.Members.Count,
                w.Projects.Count,
                w.ArchivedAt != null,
                w.CreatedAt))
            .ToListAsync();
    }

    public async Task<List<AdminUserDto>?> GetUsersAsync(long currentUserId)
    {
        if (!await _dbContext.IsSystemAdminAsync(currentUserId))
        {
            return null;
        }

        return await _dbContext.Users
            .OrderByDescending(u => u.Id)
            .Select(u => new AdminUserDto(u.Id, u.Name, u.Email, u.IsSystemAdmin, u.CreatedAt))
            .ToListAsync();
    }
}
