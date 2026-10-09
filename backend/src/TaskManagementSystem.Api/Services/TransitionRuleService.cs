using Microsoft.EntityFrameworkCore;
using TaskManagementSystem.Api.Data;
using TaskManagementSystem.Api.Dtos.Git;
using TaskManagementSystem.Api.Models;

namespace TaskManagementSystem.Api.Services;

/// <summary>
/// タスク自動遷移ルールの管理と解決(M4 §8)。WS 既定は WS Admin、プロジェクト上書きは
/// Project OWNER / WS Admin が設定する。参照は CanView。すべて LINQ/EF。
/// </summary>
public class TransitionRuleService : ITransitionRuleService
{
    private readonly AppDbContext _dbContext;

    public TransitionRuleService(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<List<TransitionRuleDto>?> GetByWorkspaceAsync(long workspaceId, long currentUserId)
    {
        var access = await _dbContext.ResolveWorkspaceAccessAsync(workspaceId, currentUserId);
        if (access is null || !access.Value.CanView)
        {
            return null;
        }

        return await _dbContext.TransitionRules
            .Where(r => r.WorkspaceId == workspaceId && r.ProjectId == null)
            .OrderBy(r => r.Id)
            .Select(r => new TransitionRuleDto(r.Id, r.Trigger, r.ToStatusKey, r.Enabled))
            .ToListAsync();
    }

    public async Task<TransitionRuleOutcome> ReplaceWorkspaceAsync(
        long workspaceId, PutTransitionRulesRequest request, long currentUserId)
    {
        var access = await _dbContext.ResolveWorkspaceAccessAsync(workspaceId, currentUserId);
        if (access is null || !access.Value.CanView)
        {
            return new TransitionRuleOutcome(TransitionRuleResult.NotFound);
        }

        if (!access.Value.IsAdmin)
        {
            return new TransitionRuleOutcome(TransitionRuleResult.Forbidden);
        }

        if (!await AreStatusKeysValidAsync(workspaceId, request.Rules))
        {
            return new TransitionRuleOutcome(TransitionRuleResult.InvalidStatus);
        }

        var existing = await _dbContext.TransitionRules
            .Where(r => r.WorkspaceId == workspaceId && r.ProjectId == null)
            .ToListAsync();
        _dbContext.TransitionRules.RemoveRange(existing);
        _dbContext.TransitionRules.AddRange(request.Rules.Select(r => new TransitionRule
        {
            WorkspaceId = workspaceId,
            ProjectId = null,
            Trigger = r.Trigger,
            ToStatusKey = r.ToStatusKey,
            Enabled = r.Enabled,
        }));
        await _dbContext.SaveChangesAsync();

        return new TransitionRuleOutcome(TransitionRuleResult.Success, (await GetByWorkspaceAsync(workspaceId, currentUserId))!);
    }

    public async Task<List<TransitionRuleDto>?> GetByProjectAsync(long projectId, long currentUserId)
    {
        var access = await _dbContext.ResolveAccessAsync(projectId, currentUserId);
        if (access is null || !access.Value.CanView)
        {
            return null;
        }

        return await _dbContext.TransitionRules
            .Where(r => r.ProjectId == projectId)
            .OrderBy(r => r.Id)
            .Select(r => new TransitionRuleDto(r.Id, r.Trigger, r.ToStatusKey, r.Enabled))
            .ToListAsync();
    }

    public async Task<TransitionRuleOutcome> ReplaceProjectAsync(
        long projectId, PutTransitionRulesRequest request, long currentUserId)
    {
        var access = await _dbContext.ResolveAccessAsync(projectId, currentUserId);
        if (access is null || !access.Value.CanView)
        {
            return new TransitionRuleOutcome(TransitionRuleResult.NotFound);
        }

        if (!access.Value.CanManageProject)
        {
            return new TransitionRuleOutcome(TransitionRuleResult.Forbidden);
        }

        var workspaceId = await _dbContext.Projects
            .Where(p => p.Id == projectId).Select(p => p.WorkspaceId).FirstAsync();
        if (!await AreStatusKeysValidAsync(workspaceId, request.Rules))
        {
            return new TransitionRuleOutcome(TransitionRuleResult.InvalidStatus);
        }

        var existing = await _dbContext.TransitionRules
            .Where(r => r.ProjectId == projectId)
            .ToListAsync();
        _dbContext.TransitionRules.RemoveRange(existing);
        _dbContext.TransitionRules.AddRange(request.Rules.Select(r => new TransitionRule
        {
            WorkspaceId = workspaceId,
            ProjectId = projectId,
            Trigger = r.Trigger,
            ToStatusKey = r.ToStatusKey,
            Enabled = r.Enabled,
        }));
        await _dbContext.SaveChangesAsync();

        return new TransitionRuleOutcome(TransitionRuleResult.Success, (await GetByProjectAsync(projectId, currentUserId))!);
    }

    public async Task<string?> ResolveToStatusAsync(long projectId, long workspaceId, string trigger)
    {
        // プロジェクト個別ルール(enabled)を優先
        var projectRule = await _dbContext.TransitionRules
            .Where(r => r.ProjectId == projectId && r.Trigger == trigger && r.Enabled)
            .Select(r => r.ToStatusKey)
            .FirstOrDefaultAsync();
        if (projectRule is not null)
        {
            return projectRule;
        }

        return await _dbContext.TransitionRules
            .Where(r => r.WorkspaceId == workspaceId && r.ProjectId == null && r.Trigger == trigger && r.Enabled)
            .Select(r => r.ToStatusKey)
            .FirstOrDefaultAsync();
    }

    /// <summary>遷移先の状態キーがすべて当該 WS のワークフローに存在するか。</summary>
    private async Task<bool> AreStatusKeysValidAsync(long workspaceId, List<TransitionRuleInput> rules)
    {
        if (rules.Count == 0)
        {
            return true;
        }

        var keys = await _dbContext.WorkflowStates
            .Where(s => s.WorkspaceId == workspaceId)
            .Select(s => s.Key)
            .ToListAsync();
        var validKeys = keys.ToHashSet();
        return rules.All(r => validKeys.Contains(r.ToStatusKey));
    }
}
