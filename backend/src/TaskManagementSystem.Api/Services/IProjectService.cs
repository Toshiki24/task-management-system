using TaskManagementSystem.Api.Dtos.Projects;

namespace TaskManagementSystem.Api.Services;

public enum UpdateProjectResult
{
    Success,
    ProjectNotFound,
    Forbidden,
}

public enum DeleteProjectResult
{
    Success,
    ProjectNotFound,
    Forbidden,
}

public record UpdateProjectOutcome(UpdateProjectResult Result, ProjectDto? Data = null);

public interface IProjectService
{
    Task<List<ProjectDto>> GetAllAsync(long currentUserId);
    Task<ProjectDto?> GetByIdAsync(long id, long currentUserId);
    Task<ProjectDto> CreateAsync(ProjectRequest request, long creatorUserId);
    Task<UpdateProjectOutcome> UpdateAsync(long id, ProjectRequest request, long currentUserId);
    Task<DeleteProjectResult> DeleteAsync(long id, long currentUserId);
}
