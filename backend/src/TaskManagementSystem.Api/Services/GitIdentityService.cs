using Microsoft.EntityFrameworkCore;
using TaskManagementSystem.Api.Data;
using TaskManagementSystem.Api.Dtos.Git;
using TaskManagementSystem.Api.Models;

namespace TaskManagementSystem.Api.Services;

/// <summary>
/// Git ユーザー⇄アプリメンバーの対応付け管理(identity マッピング。M4 §6)。
/// 参照・作成・削除は WS Admin。対応付けはワークスペースのメンバーに限る。すべて LINQ/EF。
/// </summary>
public class GitIdentityService : IGitIdentityService
{
    private readonly AppDbContext _dbContext;

    public GitIdentityService(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<List<GitIdentityDto>?> GetByWorkspaceAsync(long workspaceId, long currentUserId)
    {
        var access = await _dbContext.ResolveWorkspaceAccessAsync(workspaceId, currentUserId);
        if (access is null || !access.Value.CanView)
        {
            return null;
        }

        if (!access.Value.IsAdmin)
        {
            return null;
        }

        return await _dbContext.GitIdentities
            .Where(i => i.WorkspaceId == workspaceId)
            .OrderBy(i => i.Id)
            .Select(i => new GitIdentityDto(
                i.Id, i.WorkspaceId, i.UserId, i.User.Name, i.User.Email,
                i.Provider, i.ExternalUserId, i.ExternalUsername))
            .ToListAsync();
    }

    public async Task<GitIdentityOutcome> CreateAsync(
        long workspaceId, CreateGitIdentityRequest request, long currentUserId)
    {
        var access = await _dbContext.ResolveWorkspaceAccessAsync(workspaceId, currentUserId);
        if (access is null || !access.Value.CanView)
        {
            return new GitIdentityOutcome(GitIdentityResult.WorkspaceNotFound);
        }

        if (!access.Value.IsAdmin)
        {
            return new GitIdentityOutcome(GitIdentityResult.Forbidden);
        }

        // 対応付けの対象はワークスペースのメンバーに限る
        var isMember = await _dbContext.WorkspaceMembers
            .AnyAsync(m => m.WorkspaceId == workspaceId && m.UserId == request.UserId);
        if (!isMember)
        {
            return new GitIdentityOutcome(GitIdentityResult.UserNotMember);
        }

        // 同一 WS・プロバイダ・外部ユーザーIDの重複を抑止(DB 一意制約と対)
        var duplicate = await _dbContext.GitIdentities.AnyAsync(i =>
            i.WorkspaceId == workspaceId
            && i.Provider == request.Provider
            && i.ExternalUserId == request.ExternalUserId);
        if (duplicate)
        {
            return new GitIdentityOutcome(GitIdentityResult.Duplicate);
        }

        var identity = new GitIdentity
        {
            WorkspaceId = workspaceId,
            UserId = request.UserId,
            Provider = request.Provider,
            ExternalUserId = request.ExternalUserId.Trim(),
            ExternalUsername = string.IsNullOrWhiteSpace(request.ExternalUsername)
                ? null : request.ExternalUsername.Trim(),
        };
        _dbContext.GitIdentities.Add(identity);
        await _dbContext.SaveChangesAsync();

        await _dbContext.RecordAuditAsync(
            currentUserId, AuditActions.GitIdentityCreated, AuditTargets.GitIdentity, identity.Id, workspaceId,
            new { identity.Provider, identity.ExternalUserId });

        // 表示用の氏名/メールを付けて返す
        var user = await _dbContext.Users
            .Where(u => u.Id == identity.UserId)
            .Select(u => new { u.Name, u.Email })
            .FirstAsync();
        return new GitIdentityOutcome(GitIdentityResult.Success, new GitIdentityDto(
            identity.Id, identity.WorkspaceId, identity.UserId, user.Name, user.Email,
            identity.Provider, identity.ExternalUserId, identity.ExternalUsername));
    }

    public async Task<GitIdentityResult> DeleteAsync(long identityId, long currentUserId)
    {
        var identity = await _dbContext.GitIdentities.FirstOrDefaultAsync(i => i.Id == identityId);
        if (identity is null)
        {
            return GitIdentityResult.IdentityNotFound;
        }

        var access = await _dbContext.ResolveWorkspaceAccessAsync(identity.WorkspaceId, currentUserId);
        if (access is null || !access.Value.CanView)
        {
            return GitIdentityResult.IdentityNotFound;
        }

        if (!access.Value.IsAdmin)
        {
            return GitIdentityResult.Forbidden;
        }

        _dbContext.GitIdentities.Remove(identity);
        await _dbContext.SaveChangesAsync();

        await _dbContext.RecordAuditAsync(
            currentUserId, AuditActions.GitIdentityDeleted, AuditTargets.GitIdentity, identity.Id,
            identity.WorkspaceId, new { identity.Provider, identity.ExternalUserId });

        return GitIdentityResult.Success;
    }

    public async Task<long?> ResolveUserIdAsync(long workspaceId, string provider, string externalUserId)
    {
        var userId = await _dbContext.GitIdentities
            .Where(i => i.WorkspaceId == workspaceId
                && i.Provider == provider
                && i.ExternalUserId == externalUserId)
            .Select(i => (long?)i.UserId)
            .FirstOrDefaultAsync();
        return userId;
    }
}
