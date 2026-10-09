using Microsoft.EntityFrameworkCore;
using TaskManagementSystem.Api.Data;
using TaskManagementSystem.Api.Dtos.Git;
using TaskManagementSystem.Api.Models;
using TaskManagementSystem.Api.Services.Git;

namespace TaskManagementSystem.Api.Services;

/// <summary>
/// タスク⇄Git オブジェクトの双方向リンク(取り込み。M4 §7)。Webhook イベントの参照
/// (#123 / TASK-123)からタスクを特定し、task_git_links を作成/更新する。actor が identity
/// マッピングで解決できればアクティビティも記録する。すべて LINQ/EF。
/// </summary>
public class GitLinkService : IGitLinkService
{
    private readonly AppDbContext _dbContext;
    private readonly IGitIdentityService _identities;

    public GitLinkService(AppDbContext dbContext, IGitIdentityService identities)
    {
        _dbContext = dbContext;
        _identities = identities;
    }

    public async Task<List<TaskGitLinkDto>?> GetByTaskAsync(long taskId, long currentUserId)
    {
        var access = await _dbContext.ResolveTaskAccessAsync(taskId, currentUserId);
        if (access is null || !access.Value.CanView)
        {
            return null;
        }

        return await _dbContext.TaskGitLinks
            .Where(l => l.TaskId == taskId)
            .OrderByDescending(l => l.Id)
            .Select(l => new TaskGitLinkDto(l.Id, l.TaskId, l.LinkType, l.ExternalRef, l.Url, l.Title, l.State))
            .ToListAsync();
    }

    public async Task<int> ApplyEventAsync(GitLinkContext context, GitEvent gitEvent, CancellationToken ct = default)
    {
        var mapping = MapEvent(gitEvent.Type);
        if (mapping is null || string.IsNullOrWhiteSpace(gitEvent.Ref))
        {
            // 対象外イベント、または参照(ブランチ名/番号/SHA)が無いものは取り込まない
            return 0;
        }

        var (linkType, state, verb) = mapping.Value;

        // 参照(#123 / TASK-123)で指定されたタスクのうち、この連携リポジトリと同じプロジェクトのものだけを対象にする
        var hintedIds = gitEvent.TaskHints
            .Select(h => long.TryParse(h, out var id) ? id : (long?)null)
            .Where(id => id.HasValue).Select(id => id!.Value).Distinct().ToList();
        if (hintedIds.Count == 0)
        {
            return 0;
        }

        var taskIds = await _dbContext.Tasks
            .Where(t => t.ProjectId == context.ProjectId && hintedIds.Contains(t.Id))
            .Select(t => t.Id)
            .ToListAsync(ct);
        if (taskIds.Count == 0)
        {
            return 0;
        }

        // actor が identity マッピングで解決できればアクティビティの実行者にする(未解決なら活動は記録しない)
        long? actorUserId = null;
        if (gitEvent.Actor is not null)
        {
            actorUserId = await _identities.ResolveUserIdAsync(
                context.WorkspaceId, context.Provider, gitEvent.Actor.ExternalUserId);
        }

        foreach (var taskId in taskIds)
        {
            var existing = await _dbContext.TaskGitLinks.FirstOrDefaultAsync(
                l => l.RepositoryLinkId == context.RepositoryLinkId
                    && l.LinkType == linkType
                    && l.ExternalRef == gitEvent.Ref
                    && l.TaskId == taskId, ct);

            if (existing is null)
            {
                _dbContext.TaskGitLinks.Add(new TaskGitLink
                {
                    TaskId = taskId,
                    RepositoryLinkId = context.RepositoryLinkId,
                    LinkType = linkType,
                    ExternalRef = gitEvent.Ref!,
                    Url = gitEvent.Url,
                    Title = gitEvent.Title,
                    State = state,
                });
            }
            else
            {
                // 既存リンクは状態(OPEN→MERGED 等)とタイトル/URL を最新へ更新する
                existing.State = state;
                existing.Title = gitEvent.Title ?? existing.Title;
                existing.Url = gitEvent.Url ?? existing.Url;
            }

            if (actorUserId is { } actor)
            {
                ActivityRecorder.Record(_dbContext, context.ProjectId, taskId, actor, verb,
                    new { gitEvent.Ref, gitEvent.Title, gitEvent.Url });
            }
        }

        await _dbContext.SaveChangesAsync(ct);
        return taskIds.Count;
    }

    /// <summary>イベント種別→(リンク種別, PR/MR 状態, アクティビティ verb)。対象外は null。</summary>
    private static (string LinkType, string? State, string Verb)? MapEvent(GitEventType type) => type switch
    {
        GitEventType.BranchCreated => (GitLinkType.Branch, null, ActivityVerb.GitBranchCreated),
        GitEventType.PrOpened => (GitLinkType.PullRequest, GitLinkState.Open, ActivityVerb.GitPrOpened),
        GitEventType.PrMerged => (GitLinkType.PullRequest, GitLinkState.Merged, ActivityVerb.GitPrMerged),
        GitEventType.PrClosed => (GitLinkType.PullRequest, GitLinkState.Closed, ActivityVerb.GitPrClosed),
        GitEventType.MrOpened => (GitLinkType.MergeRequest, GitLinkState.Open, ActivityVerb.GitPrOpened),
        GitEventType.MrMerged => (GitLinkType.MergeRequest, GitLinkState.Merged, ActivityVerb.GitPrMerged),
        GitEventType.MrClosed => (GitLinkType.MergeRequest, GitLinkState.Closed, ActivityVerb.GitPrClosed),
        GitEventType.Push => (GitLinkType.Commit, null, ActivityVerb.GitCommitLinked),
        _ => null,
    };
}
