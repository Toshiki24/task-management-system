using Microsoft.EntityFrameworkCore;
using Npgsql;
using TaskManagementSystem.Api.Data;
using TaskManagementSystem.Api.Dtos.System;
using TaskManagementSystem.Api.Models;

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
            .Select(u => new AdminUserDto(
                u.Id,
                u.Name,
                u.Email,
                u.IsSystemAdmin,
                _dbContext.UserMfas.Any(m => m.UserId == u.Id && m.IsEnabled),
                u.CreatedAt))
            .ToListAsync();
    }

    public async Task<CreateManagedUserOutcome> CreateUserAsync(long currentUserId, CreateManagedUserRequest request)
    {
        if (!await _dbContext.IsSystemAdminAsync(currentUserId))
        {
            return new CreateManagedUserOutcome(CreateManagedUserResult.Forbidden);
        }

        var email = request.Email.Trim();

        if (!await _dbContext.Workspaces.AnyAsync(w => w.Id == request.WorkspaceId))
        {
            return new CreateManagedUserOutcome(CreateManagedUserResult.WorkspaceNotFound);
        }

        // 既存メールは拒否する(既存ユーザーの WS 追加は通常のメンバー管理で行う)
        if (await _dbContext.Users.AnyAsync(u => u.Email == email))
        {
            return new CreateManagedUserOutcome(CreateManagedUserResult.EmailTaken);
        }

        // 名前は任意。未指定ならメールのローカル部を使う
        var name = string.IsNullOrWhiteSpace(request.Name) ? email.Split('@')[0] : request.Name.Trim();

        var user = new User
        {
            Name = name,
            Email = email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            IsSystemAdmin = false,
        };

        try
        {
            await using var transaction = await _dbContext.Database.BeginTransactionAsync();
            _dbContext.Users.Add(user);
            await _dbContext.SaveChangesAsync();

            _dbContext.WorkspaceMembers.Add(new WorkspaceMember
            {
                WorkspaceId = request.WorkspaceId,
                UserId = user.Id,
                Role = request.Role,
            });
            await _dbContext.SaveChangesAsync();
            await transaction.CommitAsync();
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            // メールの同時登録など
            return new CreateManagedUserOutcome(CreateManagedUserResult.EmailTaken);
        }

        // 監査: ワークスペースにメンバーを追加したものとして記録(作成者・対象・WSを残す)
        await _dbContext.RecordAuditAsync(
            currentUserId, AuditActions.MemberAdded, AuditTargets.User, user.Id, request.WorkspaceId);

        return new CreateManagedUserOutcome(
            CreateManagedUserResult.Success, new SystemAdminDto(user.Id, user.Name, user.Email));
    }

    private static bool IsUniqueViolation(Exception ex)
    {
        for (var current = ex; current is not null; current = current.InnerException)
        {
            if (current is PostgresException { SqlState: "23505" })
            {
                return true;
            }
        }

        return false;
    }
}
