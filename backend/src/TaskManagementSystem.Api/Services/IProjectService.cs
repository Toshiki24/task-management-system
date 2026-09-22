using TaskManagementSystem.Api.Dtos.Projects;

namespace TaskManagementSystem.Api.Services;

public interface IProjectService
{
    Task<List<ProjectDto>> GetAllAsync();
    Task<ProjectDto?> GetByIdAsync(long id);
    Task<ProjectDto> CreateAsync(ProjectRequest request);
    Task<ProjectDto?> UpdateAsync(long id, ProjectRequest request);
    Task<bool> DeleteAsync(long id);
}
