using TaskManagementSystem.Api.Dtos.Git;

namespace TaskManagementSystem.Api.Services;

public enum GitConnectionResult
{
    Success,
    WorkspaceNotFound,
    ConnectionNotFound,
    Forbidden,
    Duplicate,
    ProviderUnavailable,
    TestFailed,
}

public record GitConnectionOutcome(GitConnectionResult Result, GitConnectionDto? Data = null);

public interface IGitConnectionService
{
    Task<List<GitConnectionDto>?> GetByWorkspaceAsync(long workspaceId, long currentUserId);
    Task<GitConnectionOutcome> CreateAsync(long workspaceId, CreateGitConnectionRequest request, long currentUserId);
    Task<GitConnectionOutcome> UpdateAsync(long connectionId, UpdateGitConnectionRequest request, long currentUserId);
    Task<GitConnectionResult> DeleteAsync(long connectionId, long currentUserId);

    /// <summary>接続の疎通確認を行い、状態(ACTIVE/ERROR)を更新する(M4 §4)。WS Admin。</summary>
    Task<GitConnectionResult> TestAsync(long connectionId, long currentUserId);
}
