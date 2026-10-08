using Microsoft.EntityFrameworkCore;
using TaskManagementSystem.Api.Data;
using TaskManagementSystem.Api.Dtos.Milestones;
using TaskManagementSystem.Api.Models;

namespace TaskManagementSystem.Api.Services;

/// <summary>
/// マイルストーン/リリースの管理とタスク割り当て(M3 §8)。参照は CanView、作成・編集・削除は
/// CanManageProject(Project OWNER / WS Admin)、割り当ては CanWrite。すべて LINQ/EF。
/// </summary>
public class MilestoneService : IMilestoneService
{
    private readonly AppDbContext _dbContext;

    public MilestoneService(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<List<MilestoneDto>?> GetByProjectAsync(long projectId, long currentUserId)
    {
        var access = await _dbContext.ResolveAccessAsync(projectId, currentUserId);
        if (access is null || !access.Value.CanView)
        {
            return null;
        }

        var milestones = await _dbContext.Milestones
            .Where(m => m.ProjectId == projectId)
            .OrderBy(m => m.DueDate).ThenBy(m => m.Id)
            .Select(m => new { m.Id, m.ProjectId, m.Name, m.DueDate, m.Status })
            .ToListAsync();
        if (milestones.Count == 0)
        {
            return new List<MilestoneDto>();
        }

        var closed = await GetClosedStatusKeysAsync(projectId);
        var milestoneIds = milestones.Select(m => m.Id).ToList();
        var taskRows = await _dbContext.Tasks
            .Where(t => t.MilestoneId != null && milestoneIds.Contains(t.MilestoneId.Value))
            .Select(t => new { MilestoneId = t.MilestoneId!.Value, t.Status, t.EstimatePoints })
            .ToListAsync();
        var byMilestone = taskRows.GroupBy(t => t.MilestoneId).ToDictionary(g => g.Key, g => g.ToList());

        return milestones.Select(m =>
        {
            var tasks = byMilestone.GetValueOrDefault(m.Id, new());
            var progress = new MilestoneProgress(
                tasks.Count(t => closed.Contains(t.Status)),
                tasks.Count,
                tasks.Sum(t => t.EstimatePoints ?? 0));
            return new MilestoneDto(m.Id, m.ProjectId, m.Name, m.DueDate, m.Status, progress);
        }).ToList();
    }

    public async Task<MilestoneOutcome> CreateAsync(long projectId, MilestoneRequest request, long currentUserId)
    {
        var access = await _dbContext.ResolveAccessAsync(projectId, currentUserId);
        if (access is null || !access.Value.CanView)
        {
            return new MilestoneOutcome(MilestoneResult.ProjectNotFound);
        }

        if (!access.Value.CanManageProject)
        {
            return new MilestoneOutcome(MilestoneResult.Forbidden);
        }

        var milestone = new Milestone
        {
            ProjectId = projectId,
            Name = request.Name,
            DueDate = request.DueDate,
            Status = request.Status,
        };
        _dbContext.Milestones.Add(milestone);
        await _dbContext.SaveChangesAsync();

        return new MilestoneOutcome(MilestoneResult.Success, ToDto(milestone, new MilestoneProgress(0, 0, 0)));
    }

    public async Task<MilestoneOutcome> UpdateAsync(long milestoneId, MilestoneRequest request, long currentUserId)
    {
        var milestone = await _dbContext.Milestones.FirstOrDefaultAsync(m => m.Id == milestoneId);
        if (milestone is null)
        {
            return new MilestoneOutcome(MilestoneResult.MilestoneNotFound);
        }

        var access = await _dbContext.ResolveAccessAsync(milestone.ProjectId, currentUserId);
        if (access is null || !access.Value.CanView)
        {
            return new MilestoneOutcome(MilestoneResult.MilestoneNotFound);
        }

        if (!access.Value.CanManageProject)
        {
            return new MilestoneOutcome(MilestoneResult.Forbidden);
        }

        milestone.Name = request.Name;
        milestone.DueDate = request.DueDate;
        milestone.Status = request.Status;
        await _dbContext.SaveChangesAsync();

        return new MilestoneOutcome(MilestoneResult.Success, ToDto(milestone, await ProgressAsync(milestone)));
    }

    public async Task<MilestoneResult> DeleteAsync(long milestoneId, long currentUserId)
    {
        var milestone = await _dbContext.Milestones.FirstOrDefaultAsync(m => m.Id == milestoneId);
        if (milestone is null)
        {
            return MilestoneResult.MilestoneNotFound;
        }

        var access = await _dbContext.ResolveAccessAsync(milestone.ProjectId, currentUserId);
        if (access is null || !access.Value.CanView)
        {
            return MilestoneResult.MilestoneNotFound;
        }

        if (!access.Value.CanManageProject)
        {
            return MilestoneResult.Forbidden;
        }

        // タスクの milestone_id は FK の SET NULL で未割り当てへ戻る
        _dbContext.Milestones.Remove(milestone);
        await _dbContext.SaveChangesAsync();
        return MilestoneResult.Success;
    }

    public async Task<MilestoneResult> AssignTaskAsync(long projectId, long taskId, long? milestoneId, long currentUserId)
    {
        var access = await _dbContext.ResolveAccessAsync(projectId, currentUserId);
        if (access is null || !access.Value.CanView)
        {
            return MilestoneResult.ProjectNotFound;
        }

        if (!access.Value.CanWrite)
        {
            return MilestoneResult.Forbidden;
        }

        var task = await _dbContext.Tasks.FirstOrDefaultAsync(t => t.Id == taskId && t.ProjectId == projectId);
        if (task is null)
        {
            return MilestoneResult.InvalidTask;
        }

        // 割り当て先マイルストーンは同一プロジェクトのもの(null は未割り当て)
        if (milestoneId is { } id && !await _dbContext.Milestones.AnyAsync(m => m.Id == id && m.ProjectId == projectId))
        {
            return MilestoneResult.InvalidMilestone;
        }

        task.MilestoneId = milestoneId;
        await _dbContext.SaveChangesAsync();
        return MilestoneResult.Success;
    }

    private static MilestoneDto ToDto(Milestone milestone, MilestoneProgress progress) =>
        new(milestone.Id, milestone.ProjectId, milestone.Name, milestone.DueDate, milestone.Status, progress);

    private async Task<MilestoneProgress> ProgressAsync(Milestone milestone)
    {
        var closed = await GetClosedStatusKeysAsync(milestone.ProjectId);
        var tasks = await _dbContext.Tasks
            .Where(t => t.MilestoneId == milestone.Id)
            .Select(t => new { t.Status, t.EstimatePoints })
            .ToListAsync();
        return new MilestoneProgress(
            tasks.Count(t => closed.Contains(t.Status)),
            tasks.Count,
            tasks.Sum(t => t.EstimatePoints ?? 0));
    }

    /// <summary>プロジェクトの属する WS で「完了」とみなす状態キー(カテゴリ DONE/CANCELLED)。</summary>
    private async Task<HashSet<string>> GetClosedStatusKeysAsync(long projectId)
    {
        var workspaceId = await _dbContext.Projects
            .Where(p => p.Id == projectId).Select(p => p.WorkspaceId).FirstAsync();
        var keys = await _dbContext.WorkflowStates
            .Where(s => s.WorkspaceId == workspaceId
                && (s.Category == WorkflowStateCategory.Done || s.Category == WorkflowStateCategory.Cancelled))
            .Select(s => s.Key)
            .ToListAsync();
        return keys.ToHashSet();
    }
}
