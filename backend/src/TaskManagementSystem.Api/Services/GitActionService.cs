using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using TaskManagementSystem.Api.Data;
using TaskManagementSystem.Api.Dtos.Git;
using TaskManagementSystem.Api.Models;
using TaskManagementSystem.Api.Services.Git;

namespace TaskManagementSystem.Api.Services;

/// <summary>
/// タスクからの能動的な Git 操作(ブランチ/PR・MR 作成。M4 §7)。CanWrite 必須。プロバイダ呼び出しは
/// IGitProvider 経由(本番アダプタは §15 ステップ7/8。現状はフェイク)。作成結果は task_git_links に
/// 記録し、アクティビティも残す。すべて LINQ/EF。
/// </summary>
public class GitActionService : IGitActionService
{
    private readonly AppDbContext _dbContext;
    private readonly IGitProviderResolver _providers;

    private static readonly Regex NonSlug = new("[^a-z0-9]+", RegexOptions.Compiled);

    public GitActionService(AppDbContext dbContext, IGitProviderResolver providers)
    {
        _dbContext = dbContext;
        _providers = providers;
    }

    public async Task<GitActionOutcome> CreateBranchAsync(long taskId, CreateBranchRequest request, long currentUserId)
    {
        var prep = await PrepareAsync(taskId, request.RepositoryLinkId, currentUserId);
        if (prep.Error is { } error)
        {
            return new GitActionOutcome(error);
        }

        var (task, link, connection, provider) = prep.Value!;
        var branchName = string.IsNullOrWhiteSpace(request.BranchName)
            ? BuildBranchName(task.Id, task.Title)
            : request.BranchName.Trim();
        var fromRef = string.IsNullOrWhiteSpace(request.FromRef)
            ? (link.DefaultBranch ?? "main")
            : request.FromRef.Trim();

        GitRef gitRef;
        try
        {
            gitRef = await provider.CreateBranchAsync(ToTarget(link, connection), branchName, fromRef);
        }
        catch
        {
            return new GitActionOutcome(GitActionResult.ProviderError);
        }

        var dto = await UpsertLinkAsync(
            task, link.Id, GitLinkType.Branch, gitRef.Name, gitRef.Url, branchName, state: null,
            currentUserId, ActivityVerb.GitBranchCreated);
        return new GitActionOutcome(GitActionResult.Success, dto);
    }

    public async Task<GitActionOutcome> CreatePullRequestAsync(
        long taskId, CreatePullRequestRequest request, long currentUserId)
    {
        var prep = await PrepareAsync(taskId, request.RepositoryLinkId, currentUserId);
        if (prep.Error is { } error)
        {
            return new GitActionOutcome(error);
        }

        var (task, link, connection, provider) = prep.Value!;
        var targetBranch = string.IsNullOrWhiteSpace(request.TargetBranch)
            ? (link.DefaultBranch ?? "main")
            : request.TargetBranch.Trim();
        var title = string.IsNullOrWhiteSpace(request.Title) ? task.Title : request.Title.Trim();
        // 本文に Closes を入れておくと、マージ時にこのタスクが参照として取り込まれる(§7)
        var body = $"Closes #{task.Id}";

        GitPullRequest pr;
        try
        {
            pr = await provider.CreatePullRequestAsync(
                ToTarget(link, connection),
                new CreatePrInput(request.SourceBranch.Trim(), targetBranch, title, body));
        }
        catch
        {
            return new GitActionOutcome(GitActionResult.ProviderError);
        }

        var linkType = connection.Provider == GitProvider.GitLab ? GitLinkType.MergeRequest : GitLinkType.PullRequest;
        var dto = await UpsertLinkAsync(
            task, link.Id, linkType, pr.Number, pr.Url, pr.Title, pr.State,
            currentUserId, ActivityVerb.GitPrOpened);
        return new GitActionOutcome(GitActionResult.Success, dto);
    }

    private record Prep(TaskItem Task, RepositoryLink Link, GitConnection Connection, IGitProvider Provider);

    /// <summary>共通の前処理(認可・リポジトリ連携・プロバイダ解決)。</summary>
    private async Task<(Prep? Value, GitActionResult? Error)> PrepareAsync(
        long taskId, long repositoryLinkId, long currentUserId)
    {
        var access = await _dbContext.ResolveTaskAccessAsync(taskId, currentUserId);
        if (access is null || !access.Value.CanView)
        {
            return (null, GitActionResult.TaskNotFound);
        }

        if (!access.Value.CanWrite)
        {
            return (null, GitActionResult.Forbidden);
        }

        var task = await _dbContext.Tasks.FirstAsync(t => t.Id == taskId);

        // 連携リポジトリはタスクと同じプロジェクトのものに限る
        var link = await _dbContext.RepositoryLinks
            .FirstOrDefaultAsync(r => r.Id == repositoryLinkId && r.ProjectId == task.ProjectId);
        if (link is null)
        {
            return (null, GitActionResult.InvalidRepositoryLink);
        }

        var connection = await _dbContext.GitConnections.FirstAsync(c => c.Id == link.GitConnectionId);
        var provider = _providers.Resolve(connection.Provider);
        if (provider is null)
        {
            return (null, GitActionResult.ProviderUnavailable);
        }

        return (new Prep(task, link, connection, provider), null);
    }

    private static RepositoryTarget ToTarget(RepositoryLink link, GitConnection connection) =>
        new(new GitConnectionRef(connection.Id, connection.BaseUrl, connection.SecretRef),
            link.ExternalRepoId, link.RepoFullName);

    /// <summary>task_git_links を作成/更新し、アクティビティを記録して DTO を返す。</summary>
    private async Task<TaskGitLinkDto> UpsertLinkAsync(
        TaskItem task, long repositoryLinkId, string linkType, string externalRef, string? url, string? title,
        string? state, long actorUserId, string verb)
    {
        var existing = await _dbContext.TaskGitLinks.FirstOrDefaultAsync(
            l => l.TaskId == task.Id && l.RepositoryLinkId == repositoryLinkId
                && l.LinkType == linkType && l.ExternalRef == externalRef);

        TaskGitLink link;
        if (existing is null)
        {
            link = new TaskGitLink
            {
                TaskId = task.Id,
                RepositoryLinkId = repositoryLinkId,
                LinkType = linkType,
                ExternalRef = externalRef,
                Url = url,
                Title = title,
                State = state,
            };
            _dbContext.TaskGitLinks.Add(link);
        }
        else
        {
            existing.Url = url ?? existing.Url;
            existing.Title = title ?? existing.Title;
            existing.State = state;
            link = existing;
        }

        ActivityRecorder.Record(_dbContext, task.ProjectId, task.Id, actorUserId, verb,
            new { externalRef, url, title });

        await _dbContext.SaveChangesAsync();
        return new TaskGitLinkDto(link.Id, link.TaskId, link.LinkType, link.ExternalRef, link.Url, link.Title, link.State);
    }

    /// <summary>規約に沿ったブランチ名 feature/{taskId}-{slug} を作る。slug が空なら feature/{taskId}。</summary>
    private static string BuildBranchName(long taskId, string title)
    {
        var slug = NonSlug.Replace(title.ToLowerInvariant(), "-").Trim('-');
        return string.IsNullOrEmpty(slug) ? $"feature/{taskId}" : $"feature/{taskId}-{slug}";
    }
}
