using TaskManagementSystem.Api.Dtos.Views;

namespace TaskManagementSystem.Api.Services;

public enum CreateSavedViewResult
{
    Success,
    WorkspaceNotFound,
    Forbidden,
}

public enum UpdateSavedViewResult
{
    Success,
    NotFound,
    Forbidden,
}

public enum DeleteSavedViewResult
{
    Success,
    NotFound,
    Forbidden,
}

public record CreateSavedViewOutcome(CreateSavedViewResult Result, SavedViewDto? Data = null);

public record UpdateSavedViewOutcome(UpdateSavedViewResult Result, SavedViewDto? Data = null);

public interface ISavedViewService
{
    Task<List<SavedViewDto>?> GetByWorkspaceAsync(long workspaceId, long currentUserId);
    Task<CreateSavedViewOutcome> CreateAsync(long workspaceId, SavedViewRequest request, long currentUserId);
    Task<UpdateSavedViewOutcome> UpdateAsync(long workspaceId, long viewId, SavedViewRequest request, long currentUserId);
    Task<DeleteSavedViewResult> DeleteAsync(long workspaceId, long viewId, long currentUserId);
}
