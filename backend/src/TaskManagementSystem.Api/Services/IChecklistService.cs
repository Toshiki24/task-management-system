using TaskManagementSystem.Api.Dtos.Tasks;

namespace TaskManagementSystem.Api.Services;

public enum CreateChecklistItemResult
{
    Success,
    TaskNotFound,
    Forbidden,
}

public enum UpdateChecklistItemResult
{
    Success,
    NotFound,
    Forbidden,
}

public enum DeleteChecklistItemResult
{
    Success,
    NotFound,
    Forbidden,
}

public record CreateChecklistItemOutcome(CreateChecklistItemResult Result, ChecklistItemDto? Data = null);

public record UpdateChecklistItemOutcome(UpdateChecklistItemResult Result, ChecklistItemDto? Data = null);

public interface IChecklistService
{
    Task<List<ChecklistItemDto>?> GetByTaskAsync(long taskId, long currentUserId);
    Task<CreateChecklistItemOutcome> CreateAsync(long taskId, ChecklistItemRequest request, long currentUserId);
    Task<UpdateChecklistItemOutcome> UpdateAsync(long taskId, long itemId, ChecklistItemRequest request, long currentUserId);
    Task<DeleteChecklistItemResult> DeleteAsync(long taskId, long itemId, long currentUserId);
}
