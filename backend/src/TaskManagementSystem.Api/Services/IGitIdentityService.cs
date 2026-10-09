using TaskManagementSystem.Api.Dtos.Git;

namespace TaskManagementSystem.Api.Services;

public enum GitIdentityResult
{
    Success,
    WorkspaceNotFound,
    IdentityNotFound,
    Forbidden,
    UserNotMember,
    Duplicate,
}

public record GitIdentityOutcome(GitIdentityResult Result, GitIdentityDto? Data = null);

public interface IGitIdentityService
{
    Task<List<GitIdentityDto>?> GetByWorkspaceAsync(long workspaceId, long currentUserId);
    Task<GitIdentityOutcome> CreateAsync(long workspaceId, CreateGitIdentityRequest request, long currentUserId);
    Task<GitIdentityResult> DeleteAsync(long identityId, long currentUserId);

    /// <summary>
    /// Git の actor(プロバイダ＋外部ユーザーID)を、ワークスペース内のアプリメンバーへ解決する。
    /// 対応付けが無ければ null(後続ステップの帰属解決で使用)。
    /// </summary>
    Task<long?> ResolveUserIdAsync(long workspaceId, string provider, string externalUserId);
}
