using TaskManagementSystem.Api.Dtos.Milestones;

namespace TaskManagementSystem.Api.Services;

public enum MilestoneResult
{
    Success,
    ProjectNotFound,
    MilestoneNotFound,
    Forbidden,
    InvalidTask,
    InvalidMilestone,
}

public record MilestoneOutcome(MilestoneResult Result, MilestoneDto? Data = null);

public interface IMilestoneService
{
    Task<List<MilestoneDto>?> GetByProjectAsync(long projectId, long currentUserId);
    Task<MilestoneOutcome> CreateAsync(long projectId, MilestoneRequest request, long currentUserId);
    Task<MilestoneOutcome> UpdateAsync(long milestoneId, MilestoneRequest request, long currentUserId);
    Task<MilestoneResult> DeleteAsync(long milestoneId, long currentUserId);

    /// <summary>タスクをマイルストーンへ割り当て/解除する(null=未割り当て)。</summary>
    Task<MilestoneResult> AssignTaskAsync(long projectId, long taskId, long? milestoneId, long currentUserId);
}
