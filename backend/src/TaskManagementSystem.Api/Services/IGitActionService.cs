using TaskManagementSystem.Api.Dtos.Git;

namespace TaskManagementSystem.Api.Services;

public enum GitActionResult
{
    Success,
    TaskNotFound,
    Forbidden,
    InvalidRepositoryLink,
    ProviderUnavailable,
    ProviderError,
}

public record GitActionOutcome(GitActionResult Result, TaskGitLinkDto? Data = null);

public interface IGitActionService
{
    /// <summary>タスクからブランチを作成し、task_git_links に記録する(M4 §7)。CanWrite。</summary>
    Task<GitActionOutcome> CreateBranchAsync(long taskId, CreateBranchRequest request, long currentUserId);

    /// <summary>タスクから PR/MR を作成し、task_git_links に記録する(M4 §7)。CanWrite。</summary>
    Task<GitActionOutcome> CreatePullRequestAsync(long taskId, CreatePullRequestRequest request, long currentUserId);
}
