using Microsoft.EntityFrameworkCore;
using TaskManagementSystem.Api.Data;
using TaskManagementSystem.Api.Dtos.Git;
using TaskManagementSystem.Api.Models;

namespace TaskManagementSystem.Api.Services;

/// <summary>
/// プロジェクト⇄リポジトリの連携管理(M4 §2)。参照は CanView、作成・削除は CanManageProject
/// (Project OWNER / WS Admin)。連携先の接続は同一ワークスペースのものに限る。すべて LINQ/EF。
/// </summary>
public class RepositoryLinkService : IRepositoryLinkService
{
    private readonly AppDbContext _dbContext;

    public RepositoryLinkService(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<List<RepositoryLinkDto>?> GetByProjectAsync(long projectId, long currentUserId)
    {
        var access = await _dbContext.ResolveAccessAsync(projectId, currentUserId);
        if (access is null || !access.Value.CanView)
        {
            return null;
        }

        return await _dbContext.RepositoryLinks
            .Where(r => r.ProjectId == projectId)
            .OrderBy(r => r.Id)
            .Select(r => new RepositoryLinkDto(
                r.Id, r.ProjectId, r.GitConnectionId, r.ExternalRepoId, r.RepoFullName, r.DefaultBranch))
            .ToListAsync();
    }

    public async Task<RepositoryLinkOutcome> CreateAsync(
        long projectId, CreateRepositoryLinkRequest request, long currentUserId)
    {
        var access = await _dbContext.ResolveAccessAsync(projectId, currentUserId);
        if (access is null || !access.Value.CanView)
        {
            return new RepositoryLinkOutcome(RepositoryLinkResult.ProjectNotFound);
        }

        if (!access.Value.CanManageProject)
        {
            return new RepositoryLinkOutcome(RepositoryLinkResult.Forbidden);
        }

        // 連携先の接続は、プロジェクトと同じワークスペースのものでなければならない
        var projectWorkspaceId = await _dbContext.Projects
            .Where(p => p.Id == projectId).Select(p => p.WorkspaceId).FirstAsync();
        var connectionValid = await _dbContext.GitConnections.AnyAsync(c =>
            c.Id == request.GitConnectionId && c.WorkspaceId == projectWorkspaceId);
        if (!connectionValid)
        {
            return new RepositoryLinkOutcome(RepositoryLinkResult.InvalidConnection);
        }

        // 同じ接続・同じリポジトリの重複連携を抑止
        var duplicate = await _dbContext.RepositoryLinks.AnyAsync(r =>
            r.GitConnectionId == request.GitConnectionId && r.ExternalRepoId == request.ExternalRepoId);
        if (duplicate)
        {
            return new RepositoryLinkOutcome(RepositoryLinkResult.Duplicate);
        }

        var link = new RepositoryLink
        {
            ProjectId = projectId,
            GitConnectionId = request.GitConnectionId,
            ExternalRepoId = request.ExternalRepoId.Trim(),
            RepoFullName = request.RepoFullName.Trim(),
            DefaultBranch = string.IsNullOrWhiteSpace(request.DefaultBranch) ? null : request.DefaultBranch.Trim(),
        };
        _dbContext.RepositoryLinks.Add(link);
        await _dbContext.SaveChangesAsync();

        await _dbContext.RecordAuditAsync(
            currentUserId, AuditActions.RepositoryLinkCreated, AuditTargets.RepositoryLink, link.Id,
            projectWorkspaceId, new { link.RepoFullName });

        return new RepositoryLinkOutcome(RepositoryLinkResult.Success, ToDto(link));
    }

    public async Task<RepositoryLinkResult> DeleteAsync(long linkId, long currentUserId)
    {
        var link = await _dbContext.RepositoryLinks.FirstOrDefaultAsync(r => r.Id == linkId);
        if (link is null)
        {
            return RepositoryLinkResult.LinkNotFound;
        }

        var access = await _dbContext.ResolveAccessAsync(link.ProjectId, currentUserId);
        if (access is null || !access.Value.CanView)
        {
            return RepositoryLinkResult.LinkNotFound;
        }

        if (!access.Value.CanManageProject)
        {
            return RepositoryLinkResult.Forbidden;
        }

        var workspaceId = await _dbContext.Projects
            .Where(p => p.Id == link.ProjectId).Select(p => p.WorkspaceId).FirstAsync();

        // タスクへの Git リンク(task_git_links)は ON DELETE CASCADE で連動削除される
        _dbContext.RepositoryLinks.Remove(link);
        await _dbContext.SaveChangesAsync();

        await _dbContext.RecordAuditAsync(
            currentUserId, AuditActions.RepositoryLinkDeleted, AuditTargets.RepositoryLink, link.Id,
            workspaceId, new { link.RepoFullName });

        return RepositoryLinkResult.Success;
    }

    private static RepositoryLinkDto ToDto(RepositoryLink r) =>
        new(r.Id, r.ProjectId, r.GitConnectionId, r.ExternalRepoId, r.RepoFullName, r.DefaultBranch);
}
