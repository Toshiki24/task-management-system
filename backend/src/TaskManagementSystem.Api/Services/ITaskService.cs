using TaskManagementSystem.Api.Dtos.Tasks;

namespace TaskManagementSystem.Api.Services;

public enum CreateTaskResult
{
    Success,
    ProjectNotFound,
    AssigneeNotFound,
}

public enum UpdateTaskResult
{
    Success,
    TaskNotFound,
    AssigneeNotFound,
}

public record CreateTaskOutcome(CreateTaskResult Result, TaskDto? Data = null);

public record UpdateTaskOutcome(UpdateTaskResult Result, TaskDto? Data = null);

public interface ITaskService
{
    Task<List<TaskDto>?> GetByProjectAsync(long projectId);
    Task<TaskDto?> GetByIdAsync(long id);
    Task<CreateTaskOutcome> CreateAsync(long projectId, TaskRequest request);
    Task<UpdateTaskOutcome> UpdateAsync(long id, TaskRequest request);
    Task<bool> DeleteAsync(long id);
}
