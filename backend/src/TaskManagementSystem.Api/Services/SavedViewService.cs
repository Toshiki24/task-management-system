using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TaskManagementSystem.Api.Data;
using TaskManagementSystem.Api.Dtos.Views;
using TaskManagementSystem.Api.Models;

namespace TaskManagementSystem.Api.Services;

/// <summary>
/// 保存ビュー(ワークスペース単位)の管理(Phase 2 M2 §5.2)。
/// 個人用は本人のみ、共有は同一WSの所属者に見える。可視性は M1 の WorkspaceAccess に集約。
/// </summary>
public class SavedViewService : ISavedViewService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private readonly AppDbContext _dbContext;

    public SavedViewService(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<List<SavedViewDto>?> GetByWorkspaceAsync(long workspaceId, long currentUserId)
    {
        var access = await _dbContext.ResolveWorkspaceAccessAsync(workspaceId, currentUserId);
        if (access is null || !access.Value.CanView)
        {
            return null;
        }

        // 自分の個人ビュー＋共有ビューのみ(他人の個人ビューは出さない)
        var views = await _dbContext.SavedViews
            .Where(v => v.WorkspaceId == workspaceId && (v.OwnerUserId == currentUserId || v.IsShared))
            .OrderBy(v => v.Name)
            .ToListAsync();

        return views.Select(v => ToDto(v, currentUserId)).ToList();
    }

    public async Task<CreateSavedViewOutcome> CreateAsync(
        long workspaceId, SavedViewRequest request, long currentUserId)
    {
        var access = await _dbContext.ResolveWorkspaceAccessAsync(workspaceId, currentUserId);
        if (access is null || !access.Value.CanView)
        {
            // 非所属は存在を開示しない(404 相当)
            return new CreateSavedViewOutcome(CreateSavedViewResult.WorkspaceNotFound);
        }

        var view = new SavedView
        {
            WorkspaceId = workspaceId,
            OwnerUserId = currentUserId,
            Name = request.Name,
            ViewType = request.ViewType,
            IsShared = request.IsShared,
            Filters = SerializeFilters(request.Filters),
        };

        _dbContext.SavedViews.Add(view);
        await _dbContext.SaveChangesAsync();

        return new CreateSavedViewOutcome(CreateSavedViewResult.Success, ToDto(view, currentUserId));
    }

    public async Task<UpdateSavedViewOutcome> UpdateAsync(
        long workspaceId, long viewId, SavedViewRequest request, long currentUserId)
    {
        var access = await _dbContext.ResolveWorkspaceAccessAsync(workspaceId, currentUserId);
        if (access is null || !access.Value.CanView)
        {
            return new UpdateSavedViewOutcome(UpdateSavedViewResult.NotFound);
        }

        var view = await _dbContext.SavedViews
            .FirstOrDefaultAsync(v => v.Id == viewId && v.WorkspaceId == workspaceId);
        if (view is null)
        {
            return new UpdateSavedViewOutcome(UpdateSavedViewResult.NotFound);
        }

        // 編集・削除は作成者または WS Admin
        if (view.OwnerUserId != currentUserId && !access.Value.IsAdmin)
        {
            return new UpdateSavedViewOutcome(UpdateSavedViewResult.Forbidden);
        }

        view.Name = request.Name;
        view.ViewType = request.ViewType;
        view.IsShared = request.IsShared;
        view.Filters = SerializeFilters(request.Filters);
        await _dbContext.SaveChangesAsync();

        return new UpdateSavedViewOutcome(UpdateSavedViewResult.Success, ToDto(view, currentUserId));
    }

    public async Task<DeleteSavedViewResult> DeleteAsync(long workspaceId, long viewId, long currentUserId)
    {
        var access = await _dbContext.ResolveWorkspaceAccessAsync(workspaceId, currentUserId);
        if (access is null || !access.Value.CanView)
        {
            return DeleteSavedViewResult.NotFound;
        }

        var view = await _dbContext.SavedViews
            .FirstOrDefaultAsync(v => v.Id == viewId && v.WorkspaceId == workspaceId);
        if (view is null)
        {
            return DeleteSavedViewResult.NotFound;
        }

        if (view.OwnerUserId != currentUserId && !access.Value.IsAdmin)
        {
            return DeleteSavedViewResult.Forbidden;
        }

        _dbContext.SavedViews.Remove(view);
        await _dbContext.SaveChangesAsync();

        return DeleteSavedViewResult.Success;
    }

    // 既知キーのみを保存する(未知キーは SavedViewFilters に無いため落ちる。§5.3)
    private static string SerializeFilters(SavedViewFilters? filters) =>
        JsonSerializer.Serialize(filters ?? new SavedViewFilters(null, null, null, null, null, null), JsonOptions);

    private static SavedViewFilters DeserializeFilters(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<SavedViewFilters>(json, JsonOptions)
                ?? new SavedViewFilters(null, null, null, null, null, null);
        }
        catch
        {
            return new SavedViewFilters(null, null, null, null, null, null);
        }
    }

    private static SavedViewDto ToDto(SavedView v, long currentUserId) => new(
        v.Id,
        v.WorkspaceId,
        v.Name,
        v.ViewType,
        v.IsShared,
        v.OwnerUserId == currentUserId,
        DeserializeFilters(v.Filters));
}
