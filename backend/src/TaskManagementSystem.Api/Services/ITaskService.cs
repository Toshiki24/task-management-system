using TaskManagementSystem.Api.Dtos.Tasks;

namespace TaskManagementSystem.Api.Services;

public enum CreateTaskResult
{
    Success,
    ProjectNotFound,
    AssigneeNotFound,
    AssigneeNotMember,
}

public enum UpdateTaskResult
{
    Success,
    TaskNotFound,
    AssigneeNotFound,
    AssigneeNotMember,
}

public enum DeleteTaskResult
{
    Success,
    TaskNotFound,
    Forbidden,
}

public record CreateTaskOutcome(CreateTaskResult Result, TaskDto? Data = null);

public record UpdateTaskOutcome(UpdateTaskResult Result, TaskDto? Data = null);

public interface ITaskService
{
    Task<List<TaskDto>?> GetByProjectAsync(long projectId, long currentUserId);
    Task<TaskDto?> GetByIdAsync(long id, long currentUserId);
    Task<CreateTaskOutcome> CreateAsync(long projectId, TaskRequest request, long currentUserId);
    Task<UpdateTaskOutcome> UpdateAsync(long id, TaskRequest request, long currentUserId);
    Task<DeleteTaskResult> DeleteAsync(long id, long currentUserId);
}
