using TaskManagementSystem.Api.Dtos.Common;
using TaskManagementSystem.Api.Dtos.Tasks;

namespace TaskManagementSystem.Api.Services;

public enum CreateTaskResult
{
    Success,
    ProjectNotFound,
    Forbidden,
    AssigneeNotFound,
    AssigneeNotMember,
    InvalidStatus,
    InvalidLabel,
    InvalidParent,
}

public enum UpdateTaskResult
{
    Success,
    TaskNotFound,
    Forbidden,
    AssigneeNotFound,
    AssigneeNotMember,
    InvalidStatus,
    InvalidLabel,
}

public enum DeleteTaskResult
{
    Success,
    TaskNotFound,
    Forbidden,
}

public enum MoveTaskResult
{
    Success,
    TaskNotFound,
    Forbidden,
    InvalidStatus,
}

public record CreateTaskOutcome(CreateTaskResult Result, TaskDto? Data = null);

public record UpdateTaskOutcome(UpdateTaskResult Result, TaskDto? Data = null);

public record MoveTaskOutcome(MoveTaskResult Result, TaskDto? Data = null);

public interface ITaskService
{
    Task<List<TaskDto>?> GetByProjectAsync(long projectId, long currentUserId, TaskListQuery? query = null);
    Task<PagedResult<MyTaskDto>> GetMyTasksAsync(long currentUserId, MyTasksQuery query);
    Task<TaskDto?> GetByIdAsync(long id, long currentUserId);
    Task<CreateTaskOutcome> CreateAsync(long projectId, TaskRequest request, long currentUserId);
    Task<UpdateTaskOutcome> UpdateAsync(long id, TaskRequest request, long currentUserId);
    Task<MoveTaskOutcome> MoveAsync(long id, MoveTaskRequest request, long currentUserId);
    Task<DeleteTaskResult> DeleteAsync(long id, long currentUserId);
}
