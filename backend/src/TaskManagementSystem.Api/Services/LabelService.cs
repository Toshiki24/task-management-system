using Microsoft.EntityFrameworkCore;
using TaskManagementSystem.Api.Data;
using TaskManagementSystem.Api.Dtos.Labels;
using TaskManagementSystem.Api.Models;

namespace TaskManagementSystem.Api.Services;

/// <summary>
/// ラベル(ワークスペース単位)の管理(Phase 2 M2 §8.1)。可視性・権限は M1 の WorkspaceAccess に集約。
/// すべて LINQ/EF で実装し、生 SQL は使わない。
/// </summary>
public class LabelService : ILabelService
{
    private readonly AppDbContext _dbContext;

    public LabelService(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<List<LabelDto>?> GetByWorkspaceAsync(long workspaceId, long currentUserId)
    {
        var access = await _dbContext.ResolveWorkspaceAccessAsync(workspaceId, currentUserId);
        if (access is null || !access.Value.CanView)
        {
            return null;
        }

        return await _dbContext.Labels
            .Where(l => l.WorkspaceId == workspaceId)
            .OrderBy(l => l.Name)
            .Select(l => new LabelDto(l.Id, l.WorkspaceId, l.Name, l.Color))
            .ToListAsync();
    }

    public async Task<CreateLabelOutcome> CreateAsync(long workspaceId, LabelRequest request, long currentUserId)
    {
        var access = await _dbContext.ResolveWorkspaceAccessAsync(workspaceId, currentUserId);
        if (access is null || !access.Value.CanView)
        {
            return new CreateLabelOutcome(CreateLabelResult.WorkspaceNotFound);
        }

        if (!access.Value.IsAdmin)
        {
            return new CreateLabelOutcome(CreateLabelResult.Forbidden);
        }

        if (await _dbContext.Labels.AnyAsync(l => l.WorkspaceId == workspaceId && l.Name == request.Name))
        {
            return new CreateLabelOutcome(CreateLabelResult.DuplicateName);
        }

        var label = new Label { WorkspaceId = workspaceId, Name = request.Name, Color = request.Color };
        _dbContext.Labels.Add(label);
        await _dbContext.SaveChangesAsync();

        await _dbContext.RecordAuditAsync(
            currentUserId, AuditActions.LabelCreated, AuditTargets.Label, label.Id, workspaceId,
            new { label.Name });

        return new CreateLabelOutcome(CreateLabelResult.Success, ToDto(label));
    }

    public async Task<UpdateLabelOutcome> UpdateAsync(
        long workspaceId, long labelId, LabelRequest request, long currentUserId)
    {
        var access = await _dbContext.ResolveWorkspaceAccessAsync(workspaceId, currentUserId);
        if (access is null || !access.Value.CanView)
        {
            return new UpdateLabelOutcome(UpdateLabelResult.NotFound);
        }

        if (!access.Value.IsAdmin)
        {
            return new UpdateLabelOutcome(UpdateLabelResult.Forbidden);
        }

        var label = await _dbContext.Labels
            .FirstOrDefaultAsync(l => l.Id == labelId && l.WorkspaceId == workspaceId);
        if (label is null)
        {
            return new UpdateLabelOutcome(UpdateLabelResult.NotFound);
        }

        // 同一 WS 内で別ラベルが同名を使っていないか
        if (await _dbContext.Labels.AnyAsync(
                l => l.WorkspaceId == workspaceId && l.Name == request.Name && l.Id != labelId))
        {
            return new UpdateLabelOutcome(UpdateLabelResult.DuplicateName);
        }

        label.Name = request.Name;
        label.Color = request.Color;
        await _dbContext.SaveChangesAsync();

        await _dbContext.RecordAuditAsync(
            currentUserId, AuditActions.LabelUpdated, AuditTargets.Label, label.Id, workspaceId,
            new { label.Name });

        return new UpdateLabelOutcome(UpdateLabelResult.Success, ToDto(label));
    }

    public async Task<DeleteLabelResult> DeleteAsync(long workspaceId, long labelId, long currentUserId)
    {
        var access = await _dbContext.ResolveWorkspaceAccessAsync(workspaceId, currentUserId);
        if (access is null || !access.Value.CanView)
        {
            return DeleteLabelResult.NotFound;
        }

        if (!access.Value.IsAdmin)
        {
            return DeleteLabelResult.Forbidden;
        }

        var label = await _dbContext.Labels
            .FirstOrDefaultAsync(l => l.Id == labelId && l.WorkspaceId == workspaceId);
        if (label is null)
        {
            return DeleteLabelResult.NotFound;
        }

        // タスクへの付与(task_labels)は ON DELETE CASCADE で連動削除される
        _dbContext.Labels.Remove(label);
        await _dbContext.SaveChangesAsync();

        await _dbContext.RecordAuditAsync(
            currentUserId, AuditActions.LabelDeleted, AuditTargets.Label, label.Id, workspaceId,
            new { label.Name });

        return DeleteLabelResult.Success;
    }

    private static LabelDto ToDto(Label l) => new(l.Id, l.WorkspaceId, l.Name, l.Color);
}
