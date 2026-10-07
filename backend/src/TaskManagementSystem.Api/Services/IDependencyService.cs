using TaskManagementSystem.Api.Dtos.Tasks;

namespace TaskManagementSystem.Api.Services;

public enum AddDependencyResult
{
    Success,
    TaskNotFound,
    Forbidden,
    InvalidTarget,
    InvalidRelation,
    Duplicate,
    Cycle,
}

public enum DeleteDependencyResult
{
    Success,
    NotFound,
    Forbidden,
}

public record AddDependencyOutcome(AddDependencyResult Result, DependencyLinkDto? Data = null);

public interface IDependencyService
{
    Task<TaskDependenciesDto?> GetByTaskAsync(long taskId, long currentUserId);
    Task<AddDependencyOutcome> AddAsync(long taskId, AddDependencyRequest request, long currentUserId);
    Task<DeleteDependencyResult> DeleteAsync(long taskId, long dependencyId, long currentUserId);
}
