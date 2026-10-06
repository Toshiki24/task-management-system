using Microsoft.EntityFrameworkCore;
using TaskManagementSystem.Api.Data;
using TaskManagementSystem.Api.Dtos.Workflow;
using TaskManagementSystem.Api.Models;

namespace TaskManagementSystem.Api.Services;

/// <summary>
/// ワークフロー状態(カンバンの列)の管理(Phase 2 M2 §3)。
/// 可視性・権限は M1 の WorkspaceAccess に集約した判定を用いる。すべて LINQ/EF で実装し、生 SQL は使わない。
/// </summary>
public class WorkflowStateService : IWorkflowStateService
{
    private readonly AppDbContext _dbContext;

    public WorkflowStateService(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<List<WorkflowStateDto>?> GetByWorkspaceAsync(long workspaceId, long currentUserId)
    {
        var access = await _dbContext.ResolveWorkspaceAccessAsync(workspaceId, currentUserId);
        if (access is null || !access.Value.CanView)
        {
            return null;
        }

        return await _dbContext.WorkflowStates
            .Where(s => s.WorkspaceId == workspaceId)
            .OrderBy(s => s.Position)
            .ThenBy(s => s.Id)
            .Select(s => ToDto(s))
            .ToListAsync();
    }

    public async Task<CreateWorkflowStateOutcome> CreateAsync(
        long workspaceId, WorkflowStateCreateRequest request, long currentUserId)
    {
        var access = await _dbContext.ResolveWorkspaceAccessAsync(workspaceId, currentUserId);
        if (access is null || !access.Value.CanView)
        {
            return new CreateWorkflowStateOutcome(CreateWorkflowStateResult.WorkspaceNotFound);
        }

        if (!access.Value.IsAdmin)
        {
            return new CreateWorkflowStateOutcome(CreateWorkflowStateResult.Forbidden);
        }

        var keyExists = await _dbContext.WorkflowStates
            .AnyAsync(s => s.WorkspaceId == workspaceId && s.Key == request.Key);
        if (keyExists)
        {
            return new CreateWorkflowStateOutcome(CreateWorkflowStateResult.DuplicateKey);
        }

        // 末尾に追加する(既存の最大 Position の次)
        var maxPosition = await _dbContext.WorkflowStates
            .Where(s => s.WorkspaceId == workspaceId)
            .Select(s => (int?)s.Position)
            .MaxAsync();

        var state = new WorkflowState
        {
            WorkspaceId = workspaceId,
            Key = request.Key,
            Name = request.Name,
            Category = request.Category,
            Position = (maxPosition ?? -1) + 1,
            IsDefault = false,
            Color = request.Color,
        };

        _dbContext.WorkflowStates.Add(state);
        await _dbContext.SaveChangesAsync();

        await _dbContext.RecordAuditAsync(
            currentUserId, AuditActions.WorkflowStateCreated, AuditTargets.WorkflowState, state.Id, workspaceId,
            new { state.Key, state.Name });

        return new CreateWorkflowStateOutcome(CreateWorkflowStateResult.Success, ToDto(state));
    }

    public async Task<UpdateWorkflowStateOutcome> UpdateAsync(
        long workspaceId, long stateId, WorkflowStateUpdateRequest request, long currentUserId)
    {
        var access = await _dbContext.ResolveWorkspaceAccessAsync(workspaceId, currentUserId);
        if (access is null || !access.Value.CanView)
        {
            return new UpdateWorkflowStateOutcome(UpdateWorkflowStateResult.NotFound);
        }

        if (!access.Value.IsAdmin)
        {
            return new UpdateWorkflowStateOutcome(UpdateWorkflowStateResult.Forbidden);
        }

        var state = await _dbContext.WorkflowStates
            .FirstOrDefaultAsync(s => s.Id == stateId && s.WorkspaceId == workspaceId);
        if (state is null)
        {
            return new UpdateWorkflowStateOutcome(UpdateWorkflowStateResult.NotFound);
        }

        // key は不変(タスクの status が参照するため)。名前・カテゴリ・色のみ変更する
        state.Name = request.Name;
        state.Category = request.Category;
        state.Color = request.Color;
        await _dbContext.SaveChangesAsync();

        // 並び替えが指定された場合は、対象を指定位置へ移動して WS 全体の Position を 0..n に振り直す
        if (request.Position is int target)
        {
            await ReorderAsync(workspaceId, state.Id, target);
        }

        await _dbContext.RecordAuditAsync(
            currentUserId, AuditActions.WorkflowStateUpdated, AuditTargets.WorkflowState, state.Id, workspaceId,
            new { state.Key, state.Name });

        return new UpdateWorkflowStateOutcome(UpdateWorkflowStateResult.Success, ToDto(state));
    }

    public async Task<DeleteWorkflowStateResult> DeleteAsync(
        long workspaceId, long stateId, string? moveToKey, long currentUserId)
    {
        var access = await _dbContext.ResolveWorkspaceAccessAsync(workspaceId, currentUserId);
        if (access is null || !access.Value.CanView)
        {
            return DeleteWorkflowStateResult.NotFound;
        }

        if (!access.Value.IsAdmin)
        {
            return DeleteWorkflowStateResult.Forbidden;
        }

        var state = await _dbContext.WorkflowStates
            .FirstOrDefaultAsync(s => s.Id == stateId && s.WorkspaceId == workspaceId);
        if (state is null)
        {
            return DeleteWorkflowStateResult.NotFound;
        }

        // 既定状態は新規タスクの受け皿なので削除できない
        if (state.IsDefault)
        {
            return DeleteWorkflowStateResult.DefaultState;
        }

        // この状態を使っているタスク(WS 内の全プロジェクト横断)の件数
        var tasksUsing = await _dbContext.Tasks
            .Where(t => t.Project.WorkspaceId == workspaceId && t.Status == state.Key)
            .ToListAsync();

        if (tasksUsing.Count > 0)
        {
            // 使用中は付け替え先が必須。moveToKey は同一 WS の別の状態キーであること
            if (string.IsNullOrEmpty(moveToKey) || moveToKey == state.Key)
            {
                return DeleteWorkflowStateResult.InUse;
            }

            var moveToExists = await _dbContext.WorkflowStates
                .AnyAsync(s => s.WorkspaceId == workspaceId && s.Key == moveToKey);
            if (!moveToExists)
            {
                return DeleteWorkflowStateResult.InUse;
            }

            foreach (var task in tasksUsing)
            {
                task.Status = moveToKey;
            }

            await _dbContext.SaveChangesAsync();
        }

        _dbContext.WorkflowStates.Remove(state);
        await _dbContext.SaveChangesAsync();

        // 削除後に Position を 0..n へ振り直す
        await ReindexAsync(workspaceId);

        await _dbContext.RecordAuditAsync(
            currentUserId, AuditActions.WorkflowStateDeleted, AuditTargets.WorkflowState, state.Id, workspaceId,
            new { state.Key, MovedTo = moveToKey });

        return DeleteWorkflowStateResult.Success;
    }

    /// <summary>対象状態を target の位置へ移動し、WS 全体の Position を 0..n に振り直す。</summary>
    private async Task ReorderAsync(long workspaceId, long stateId, int target)
    {
        var states = await _dbContext.WorkflowStates
            .Where(s => s.WorkspaceId == workspaceId)
            .OrderBy(s => s.Position)
            .ThenBy(s => s.Id)
            .ToListAsync();

        var moving = states.FirstOrDefault(s => s.Id == stateId);
        if (moving is null)
        {
            return;
        }

        states.Remove(moving);
        var index = Math.Clamp(target, 0, states.Count);
        states.Insert(index, moving);

        for (var i = 0; i < states.Count; i++)
        {
            states[i].Position = i;
        }

        await _dbContext.SaveChangesAsync();
    }

    /// <summary>WS の状態の Position を現在の並び順のまま 0..n へ詰め直す。</summary>
    private async Task ReindexAsync(long workspaceId)
    {
        var states = await _dbContext.WorkflowStates
            .Where(s => s.WorkspaceId == workspaceId)
            .OrderBy(s => s.Position)
            .ThenBy(s => s.Id)
            .ToListAsync();

        for (var i = 0; i < states.Count; i++)
        {
            states[i].Position = i;
        }

        await _dbContext.SaveChangesAsync();
    }

    private static WorkflowStateDto ToDto(WorkflowState s) => new(
        s.Id, s.WorkspaceId, s.Key, s.Name, s.Category, s.Position, s.IsDefault, s.Color);
}
