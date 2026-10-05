using TaskManagementSystem.Api.Dtos.Projects;

namespace TaskManagementSystem.Api.Services;

public enum CreateProjectResult
{
    Success,
    WorkspaceNotFound,
    Forbidden,
}

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

public record CreateProjectOutcome(CreateProjectResult Result, ProjectDto? Data = null);
public record UpdateProjectOutcome(UpdateProjectResult Result, ProjectDto? Data = null);

public interface IProjectService
{
    /// <summary>ワークスペース内のプロジェクト一覧。ワークスペースを閲覧できない場合は null(= 404)。</summary>
    Task<List<ProjectDto>?> GetByWorkspaceAsync(long workspaceId, long currentUserId);

    Task<ProjectDto?> GetByIdAsync(long id, long currentUserId);

    Task<CreateProjectOutcome> CreateAsync(long workspaceId, ProjectRequest request, long creatorUserId);

    Task<UpdateProjectOutcome> UpdateAsync(long id, ProjectRequest request, long currentUserId);

    Task<DeleteProjectResult> DeleteAsync(long id, long currentUserId);
}
