using Microsoft.EntityFrameworkCore;
using TaskManagementSystem.Api.Data;
using TaskManagementSystem.Api.Dtos.Cycles;
using TaskManagementSystem.Api.Models;

namespace TaskManagementSystem.Api.Services;

/// <summary>
/// サイクル(スプリント)の管理とタスク割り当て(M3 §8)。参照は CanView、作成・編集・削除は
/// CanManageProject(Project OWNER / WS Admin)、割り当ては CanWrite。すべて LINQ/EF。
/// </summary>
public class CycleService : ICycleService
{
    private readonly AppDbContext _dbContext;

    public CycleService(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<List<CycleDto>?> GetByProjectAsync(long projectId, long currentUserId)
    {
        var access = await _dbContext.ResolveAccessAsync(projectId, currentUserId);
        if (access is null || !access.Value.CanView)
        {
            return null;
        }

        var cycles = await _dbContext.Cycles
            .Where(c => c.ProjectId == projectId)
            .OrderBy(c => c.StartDate).ThenBy(c => c.Id)
            .Select(c => new { c.Id, c.ProjectId, c.Name, c.StartDate, c.EndDate, c.Status })
            .ToListAsync();
        if (cycles.Count == 0)
        {
            return new List<CycleDto>();
        }

        var closed = await GetClosedStatusKeysAsync(projectId);
        var cycleIds = cycles.Select(c => c.Id).ToList();
        var taskRows = await _dbContext.Tasks
            .Where(t => t.CycleId != null && cycleIds.Contains(t.CycleId.Value))
            .Select(t => new { CycleId = t.CycleId!.Value, t.Status, t.EstimatePoints })
            .ToListAsync();
        var byCycle = taskRows.GroupBy(t => t.CycleId).ToDictionary(g => g.Key, g => g.ToList());

        return cycles.Select(c =>
        {
            var tasks = byCycle.GetValueOrDefault(c.Id, new());
            var progress = new CycleProgress(
                tasks.Count(t => closed.Contains(t.Status)),
                tasks.Count,
                tasks.Sum(t => t.EstimatePoints ?? 0));
            return new CycleDto(c.Id, c.ProjectId, c.Name, c.StartDate, c.EndDate, c.Status, progress);
        }).ToList();
    }

    public async Task<CycleOutcome> CreateAsync(long projectId, CycleRequest request, long currentUserId)
    {
        var access = await _dbContext.ResolveAccessAsync(projectId, currentUserId);
        if (access is null || !access.Value.CanView)
        {
            return new CycleOutcome(CycleResult.ProjectNotFound);
        }

        if (!access.Value.CanManageProject)
        {
            return new CycleOutcome(CycleResult.Forbidden);
        }

        var cycle = new Cycle
        {
            ProjectId = projectId,
            Name = request.Name,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            Status = request.Status,
        };
        _dbContext.Cycles.Add(cycle);
        await _dbContext.SaveChangesAsync();

        return new CycleOutcome(CycleResult.Success, ToDto(cycle, new CycleProgress(0, 0, 0)));
    }

    public async Task<CycleOutcome> UpdateAsync(long cycleId, CycleRequest request, long currentUserId)
    {
        var cycle = await _dbContext.Cycles.FirstOrDefaultAsync(c => c.Id == cycleId);
        if (cycle is null)
        {
            return new CycleOutcome(CycleResult.CycleNotFound);
        }

        var access = await _dbContext.ResolveAccessAsync(cycle.ProjectId, currentUserId);
        if (access is null || !access.Value.CanView)
        {
            return new CycleOutcome(CycleResult.CycleNotFound);
        }

        if (!access.Value.CanManageProject)
        {
            return new CycleOutcome(CycleResult.Forbidden);
        }

        cycle.Name = request.Name;
        cycle.StartDate = request.StartDate;
        cycle.EndDate = request.EndDate;
        cycle.Status = request.Status;
        await _dbContext.SaveChangesAsync();

        return new CycleOutcome(CycleResult.Success, ToDto(cycle, await ProgressAsync(cycle)));
    }

    public async Task<CycleResult> DeleteAsync(long cycleId, long currentUserId)
    {
        var cycle = await _dbContext.Cycles.FirstOrDefaultAsync(c => c.Id == cycleId);
        if (cycle is null)
        {
            return CycleResult.CycleNotFound;
        }

        var access = await _dbContext.ResolveAccessAsync(cycle.ProjectId, currentUserId);
        if (access is null || !access.Value.CanView)
        {
            return CycleResult.CycleNotFound;
        }

        if (!access.Value.CanManageProject)
        {
            return CycleResult.Forbidden;
        }

        // タスクの cycle_id は FK の SET NULL でバックログへ戻る
        _dbContext.Cycles.Remove(cycle);
        await _dbContext.SaveChangesAsync();
        return CycleResult.Success;
    }

    public async Task<CycleResult> AssignTaskAsync(long projectId, long taskId, long? cycleId, long currentUserId)
    {
        var access = await _dbContext.ResolveAccessAsync(projectId, currentUserId);
        if (access is null || !access.Value.CanView)
        {
            return CycleResult.ProjectNotFound;
        }

        if (!access.Value.CanWrite)
        {
            return CycleResult.Forbidden;
        }

        var task = await _dbContext.Tasks.FirstOrDefaultAsync(t => t.Id == taskId && t.ProjectId == projectId);
        if (task is null)
        {
            return CycleResult.InvalidTask;
        }

        // 割り当て先サイクルは同一プロジェクトのもの(null はバックログ)
        if (cycleId is { } id && !await _dbContext.Cycles.AnyAsync(c => c.Id == id && c.ProjectId == projectId))
        {
            return CycleResult.InvalidCycle;
        }

        task.CycleId = cycleId;
        await _dbContext.SaveChangesAsync();
        return CycleResult.Success;
    }

    private static CycleDto ToDto(Cycle cycle, CycleProgress progress) =>
        new(cycle.Id, cycle.ProjectId, cycle.Name, cycle.StartDate, cycle.EndDate, cycle.Status, progress);

    private async Task<CycleProgress> ProgressAsync(Cycle cycle)
    {
        var closed = await GetClosedStatusKeysAsync(cycle.ProjectId);
        var tasks = await _dbContext.Tasks
            .Where(t => t.CycleId == cycle.Id)
            .Select(t => new { t.Status, t.EstimatePoints })
            .ToListAsync();
        return new CycleProgress(
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
