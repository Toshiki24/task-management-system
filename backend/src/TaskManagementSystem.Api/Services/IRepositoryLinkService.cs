using TaskManagementSystem.Api.Dtos.Git;

namespace TaskManagementSystem.Api.Services;

public enum RepositoryLinkResult
{
    Success,
    ProjectNotFound,
    LinkNotFound,
    Forbidden,
    InvalidConnection,
    Duplicate,
}

public record RepositoryLinkOutcome(RepositoryLinkResult Result, RepositoryLinkDto? Data = null);

public interface IRepositoryLinkService
{
    Task<List<RepositoryLinkDto>?> GetByProjectAsync(long projectId, long currentUserId);
    Task<RepositoryLinkOutcome> CreateAsync(long projectId, CreateRepositoryLinkRequest request, long currentUserId);
    Task<RepositoryLinkResult> DeleteAsync(long linkId, long currentUserId);
}
