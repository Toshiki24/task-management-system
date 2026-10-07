using Microsoft.EntityFrameworkCore;
using TaskManagementSystem.Api.Data;
using TaskManagementSystem.Api.Dtos.Tasks;
using TaskManagementSystem.Api.Models;

namespace TaskManagementSystem.Api.Services;

/// <summary>
/// タスク依存(ブロック/被ブロック。Phase 2 M2 §7.3)。参照は CanView、変更は CanWrite(Viewer 不可)。
/// 認可は M1 の ProjectAccess(ResolveTaskAccessAsync)に集約。すべて LINQ/EF(生 SQL は使わない)。
/// 依存は同一プロジェクト内・自己参照不可・循環不可。循環判定はプロジェクトの辺をメモリに読み出して到達可能性で行う。
/// </summary>
public class DependencyService : IDependencyService
{
    private readonly AppDbContext _dbContext;

    public DependencyService(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<TaskDependenciesDto?> GetByTaskAsync(long taskId, long currentUserId)
    {
        var access = await _dbContext.ResolveTaskAccessAsync(taskId, currentUserId);
        if (access is null || !access.Value.CanView)
        {
            return null;
        }

        var closed = await GetClosedStatusKeysForTaskAsync(taskId);

        // このタスクを待たせているタスク(自分が blocked 側 = 相手が blocking 側)
        var blockedBy = await _dbContext.TaskDependencies
            .Where(d => d.BlockedTaskId == taskId)
            .OrderBy(d => d.Id)
            .Select(d => new { d.Id, Task = d.BlockingTask })
            .ToListAsync();

        // このタスクが待たせているタスク(自分が blocking 側 = 相手が blocked 側)
        var blocking = await _dbContext.TaskDependencies
            .Where(d => d.BlockingTaskId == taskId)
            .OrderBy(d => d.Id)
            .Select(d => new { d.Id, Task = d.BlockedTask })
            .ToListAsync();

        return new TaskDependenciesDto(
            blockedBy.Select(x => ToLink(x.Id, x.Task, closed)).ToList(),
            blocking.Select(x => ToLink(x.Id, x.Task, closed)).ToList());
    }

    public async Task<AddDependencyOutcome> AddAsync(long taskId, AddDependencyRequest request, long currentUserId)
    {
        var access = await _dbContext.ResolveTaskAccessAsync(taskId, currentUserId);
        if (access is null || !access.Value.CanView)
        {
            return new AddDependencyOutcome(AddDependencyResult.TaskNotFound);
        }

        if (!access.Value.CanWrite)
        {
            return new AddDependencyOutcome(AddDependencyResult.Forbidden);
        }

        if (request.TaskId is not { } otherId)
        {
            return new AddDependencyOutcome(AddDependencyResult.InvalidTarget);
        }

        // 経路タスクを基準に、追加する有向辺(blocking→blocked)を決める
        long blockingId;
        long blockedId;
        switch (request.Relation)
        {
            case DependencyRelations.BlockedBy:
                blockingId = otherId;
                blockedId = taskId;
                break;
            case DependencyRelations.Blocks:
                blockingId = taskId;
                blockedId = otherId;
                break;
            default:
                return new AddDependencyOutcome(AddDependencyResult.InvalidRelation);
        }

        if (blockingId == blockedId)
        {
            return new AddDependencyOutcome(AddDependencyResult.InvalidTarget);
        }

        // 相手タスクは同一プロジェクトに実在すること(存在しない/別プロジェクトは一括して不正扱い)
        var routeProjectId = await _dbContext.Tasks
            .Where(t => t.Id == taskId).Select(t => t.ProjectId).FirstAsync();
        var otherProjectId = await _dbContext.Tasks
            .Where(t => t.Id == otherId).Select(t => (long?)t.ProjectId).FirstOrDefaultAsync();
        if (otherProjectId is null || otherProjectId.Value != routeProjectId)
        {
            return new AddDependencyOutcome(AddDependencyResult.InvalidTarget);
        }

        var exists = await _dbContext.TaskDependencies
            .AnyAsync(d => d.BlockingTaskId == blockingId && d.BlockedTaskId == blockedId);
        if (exists)
        {
            return new AddDependencyOutcome(AddDependencyResult.Duplicate);
        }

        // 循環判定: 追加しようとする辺 blocking→blocked について、
        // 既に blocked から blocking へ到達可能なら(blocked が先に blocking を待たせている)、追加すると循環する。
        if (await WouldCreateCycleAsync(routeProjectId, blockingId, blockedId))
        {
            return new AddDependencyOutcome(AddDependencyResult.Cycle);
        }

        var dependency = new TaskDependency
        {
            BlockingTaskId = blockingId,
            BlockedTaskId = blockedId,
        };
        _dbContext.TaskDependencies.Add(dependency);
        await _dbContext.SaveChangesAsync();

        // レスポンスは経路タスクから見た相手(otherId)の要約を返す
        var closed = await GetClosedStatusKeysForTaskAsync(taskId);
        var other = await _dbContext.Tasks.FirstAsync(t => t.Id == otherId);
        return new AddDependencyOutcome(AddDependencyResult.Success, ToLink(dependency.Id, other, closed));
    }

    public async Task<DeleteDependencyResult> DeleteAsync(long taskId, long dependencyId, long currentUserId)
    {
        var access = await _dbContext.ResolveTaskAccessAsync(taskId, currentUserId);
        if (access is null || !access.Value.CanView)
        {
            return DeleteDependencyResult.NotFound;
        }

        if (!access.Value.CanWrite)
        {
            return DeleteDependencyResult.Forbidden;
        }

        // 経路タスクが関与している辺のみ削除できる(他タスク間の辺は触らせない)
        var dependency = await _dbContext.TaskDependencies
            .FirstOrDefaultAsync(d => d.Id == dependencyId
                && (d.BlockingTaskId == taskId || d.BlockedTaskId == taskId));
        if (dependency is null)
        {
            return DeleteDependencyResult.NotFound;
        }

        _dbContext.TaskDependencies.Remove(dependency);
        await _dbContext.SaveChangesAsync();
        return DeleteDependencyResult.Success;
    }

    /// <summary>
    /// 辺 blocking→blocked を足すと循環するかを判定する。
    /// 既存辺を「先に完了すべき→待つ」の向き(blocking→blocked)でたどり、blocked から blocking に到達できれば循環。
    /// </summary>
    private async Task<bool> WouldCreateCycleAsync(long projectId, long blockingId, long blockedId)
    {
        // プロジェクト内の依存辺をすべて読み出す(プロジェクト単位なので小さい)
        var edges = await _dbContext.TaskDependencies
            .Where(d => d.BlockingTask.ProjectId == projectId)
            .Select(d => new { d.BlockingTaskId, d.BlockedTaskId })
            .ToListAsync();

        var successors = edges
            .GroupBy(e => e.BlockingTaskId)
            .ToDictionary(g => g.Key, g => g.Select(e => e.BlockedTaskId).ToList());

        // blocked から前進(blocking→blocked 方向)して blocking に届くか = 既存の逆経路の有無
        var visited = new HashSet<long>();
        var stack = new Stack<long>();
        stack.Push(blockedId);
        while (stack.Count > 0)
        {
            var node = stack.Pop();
            if (node == blockingId)
            {
                return true;
            }

            if (!visited.Add(node) || !successors.TryGetValue(node, out var next))
            {
                continue;
            }

            foreach (var n in next)
            {
                stack.Push(n);
            }
        }

        return false;
    }

    private static DependencyLinkDto ToLink(long dependencyId, TaskItem task, HashSet<string> closed) =>
        new(dependencyId, task.Id, task.Title, task.Status, closed.Contains(task.Status));

    /// <summary>タスクの属する WS で「完了」とみなす状態キー(カテゴリが DONE/CANCELLED)を返す。</summary>
    private async Task<HashSet<string>> GetClosedStatusKeysForTaskAsync(long taskId)
    {
        var workspaceId = await _dbContext.Tasks
            .Where(t => t.Id == taskId)
            .Select(t => t.Project.WorkspaceId)
            .FirstAsync();

        var keys = await _dbContext.WorkflowStates
            .Where(s => s.WorkspaceId == workspaceId
                && (s.Category == WorkflowStateCategory.Done || s.Category == WorkflowStateCategory.Cancelled))
            .Select(s => s.Key)
            .ToListAsync();
        return keys.ToHashSet();
    }
}
