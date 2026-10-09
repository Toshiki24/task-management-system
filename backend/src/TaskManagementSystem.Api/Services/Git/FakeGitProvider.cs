using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace TaskManagementSystem.Api.Services.Git;

/// <summary>
/// テスト・ローカル用の Git プロバイダ実装(Phase 2 M4 §14)。外部ネットワークに出ず、
/// 署名検証(HMAC-SHA256)・イベント正規化・参照抽出といった共通ロジックだけを実装する。
/// 本番の GitHub/GitLab アダプタ(§15 ステップ 7/8)が入るまでの土台として使う。
///
/// Webhook ボディは次のような単純な JSON を想定する:
/// { "type": "pr_merged", "repoId": "r1", "repoFullName": "o/r", "ref": "feature/12-x",
///   "title": "Closes #12", "url": "https://...", "actorId": "u1", "actorName": "octocat" }
/// </summary>
public class FakeGitProvider : IGitProvider
{
    private static readonly Regex TaskHintPattern =
        new(@"(?:#|TASK-)(\d+)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public string Key { get; }

    public FakeGitProvider(string key = Models.GitProvider.GitHub)
    {
        Key = key;
    }

    /// <summary>ヘッダ "x-signature" を HMAC-SHA256(body, secret) の 16 進と比較する。</summary>
    public bool VerifySignature(GitWebhookRequest request, string signingSecret)
    {
        if (!request.Headers.TryGetValue("x-signature", out var provided) || string.IsNullOrEmpty(provided))
        {
            return false;
        }

        var expected = ComputeSignature(request.RawBody, signingSecret);
        // タイミング攻撃を避けるため固定時間比較
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(provided), Encoding.UTF8.GetBytes(expected));
    }

    /// <summary>テストが署名付きリクエストを組み立てるためのヘルパ。</summary>
    public static string ComputeSignature(string rawBody, string secret)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(rawBody));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    public GitEvent? ParseEvent(GitWebhookRequest request)
    {
        JsonElement root;
        try
        {
            root = JsonDocument.Parse(request.RawBody).RootElement;
        }
        catch (JsonException)
        {
            return null;
        }

        var type = GetString(root, "type") switch
        {
            "branch_created" => GitEventType.BranchCreated,
            "pr_opened" => GitEventType.PrOpened,
            "pr_merged" => GitEventType.PrMerged,
            "pr_closed" => GitEventType.PrClosed,
            "mr_opened" => GitEventType.MrOpened,
            "mr_merged" => GitEventType.MrMerged,
            "mr_closed" => GitEventType.MrClosed,
            "push" => GitEventType.Push,
            _ => GitEventType.Unknown,
        };

        var repoId = GetString(root, "repoId");
        var repoFullName = GetString(root, "repoFullName");
        if (type == GitEventType.Unknown || repoId is null || repoFullName is null)
        {
            return null;
        }

        var title = GetString(root, "title");
        var refName = GetString(root, "ref");
        var actorId = GetString(root, "actorId");
        var actor = actorId is null ? null : new GitActor(actorId, GetString(root, "actorName"));

        return new GitEvent(
            type,
            new RepositoryRef(repoId, repoFullName),
            actor,
            refName,
            title,
            GetString(root, "url"),
            DateTimeOffset.UtcNow,
            ExtractTaskHints($"{refName} {title}"));
    }

    /// <summary>ブランチ名・コミット/PR 本文からタスク参照(#123 / TASK-123)を抽出する(§7)。</summary>
    public static IReadOnlyList<string> ExtractTaskHints(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return Array.Empty<string>();
        }

        return TaskHintPattern.Matches(text)
            .Select(m => m.Groups[1].Value)
            .Distinct()
            .ToList();
    }

    public Task<GitRef> CreateBranchAsync(RepositoryTarget repo, string branchName, string fromRef, CancellationToken ct = default) =>
        Task.FromResult(new GitRef(branchName, "fake-sha", $"https://example.test/{repo.FullName}/tree/{branchName}"));

    public Task<GitPullRequest> CreatePullRequestAsync(RepositoryTarget repo, CreatePrInput input, CancellationToken ct = default) =>
        Task.FromResult(new GitPullRequest("1", $"https://example.test/{repo.FullName}/pull/1", input.Title, GitLinkState_Open));

    public Task<IReadOnlyList<GitRepository>> ListRepositoriesAsync(GitConnectionRef connection, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<GitRepository>>(new List<GitRepository>
        {
            new("r1", "example/sample", "main"),
        });

    private const string GitLinkState_Open = "OPEN";

    private static string? GetString(JsonElement root, string name) =>
        root.TryGetProperty(name, out var el) && el.ValueKind == JsonValueKind.String ? el.GetString() : null;
}

/// <summary>複数の <see cref="IGitProvider"/> を Key で引く既定のリゾルバ。</summary>
public class GitProviderResolver : IGitProviderResolver
{
    private readonly Dictionary<string, IGitProvider> _byKey;

    public GitProviderResolver(IEnumerable<IGitProvider> providers)
    {
        _byKey = providers.ToDictionary(p => p.Key, StringComparer.OrdinalIgnoreCase);
    }

    public IGitProvider? Resolve(string providerKey) =>
        _byKey.TryGetValue(providerKey, out var provider) ? provider : null;
}
