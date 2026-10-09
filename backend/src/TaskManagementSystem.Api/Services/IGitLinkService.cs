using TaskManagementSystem.Api.Dtos.Git;
using TaskManagementSystem.Api.Services.Git;

namespace TaskManagementSystem.Api.Services;

/// <summary>Webhook イベントを適用する対象(連携リポジトリ)の文脈。</summary>
public record GitLinkContext(long RepositoryLinkId, long ProjectId, long WorkspaceId, string Provider);

public interface IGitLinkService
{
    /// <summary>タスクに紐づく Git リンク一覧(CanView)。プロジェクト非所属は null。</summary>
    Task<List<TaskGitLinkDto>?> GetByTaskAsync(long taskId, long currentUserId);

    /// <summary>
    /// 正規化済み Git イベントから task_git_links を作成/更新し、actor が解決できれば
    /// アクティビティを記録する(M4 §7。遷移は行わない)。処理したタスク数を返す。
    /// </summary>
    Task<int> ApplyEventAsync(GitLinkContext context, GitEvent gitEvent, CancellationToken ct = default);
}
