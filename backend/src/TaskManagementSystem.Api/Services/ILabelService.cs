using TaskManagementSystem.Api.Dtos.Labels;

namespace TaskManagementSystem.Api.Services;

public enum CreateLabelResult
{
    Success,
    WorkspaceNotFound,
    Forbidden,
    DuplicateName,
}

public enum UpdateLabelResult
{
    Success,
    NotFound,
    Forbidden,
    DuplicateName,
}

public enum DeleteLabelResult
{
    Success,
    NotFound,
    Forbidden,
}

public record CreateLabelOutcome(CreateLabelResult Result, LabelDto? Data = null);

public record UpdateLabelOutcome(UpdateLabelResult Result, LabelDto? Data = null);

public interface ILabelService
{
    Task<List<LabelDto>?> GetByWorkspaceAsync(long workspaceId, long currentUserId);
    Task<CreateLabelOutcome> CreateAsync(long workspaceId, LabelRequest request, long currentUserId);
    Task<UpdateLabelOutcome> UpdateAsync(long workspaceId, long labelId, LabelRequest request, long currentUserId);
    Task<DeleteLabelResult> DeleteAsync(long workspaceId, long labelId, long currentUserId);
}
