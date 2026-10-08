using TaskManagementSystem.Api.Dtos.Cycles;

namespace TaskManagementSystem.Api.Services;

public enum CycleResult
{
    Success,
    ProjectNotFound,
    CycleNotFound,
    Forbidden,
    InvalidTask,
    InvalidCycle,
}

public record CycleOutcome(CycleResult Result, CycleDto? Data = null);

public interface ICycleService
{
    Task<List<CycleDto>?> GetByProjectAsync(long projectId, long currentUserId);
    Task<CycleOutcome> CreateAsync(long projectId, CycleRequest request, long currentUserId);
    Task<CycleOutcome> UpdateAsync(long cycleId, CycleRequest request, long currentUserId);
    Task<CycleResult> DeleteAsync(long cycleId, long currentUserId);

    /// <summary>タスクをサイクルへ割り当て/解除する(null=バックログ)。</summary>
    Task<CycleResult> AssignTaskAsync(long projectId, long taskId, long? cycleId, long currentUserId);
}
