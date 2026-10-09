using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace TaskManagementSystem.Api.Services.Git;

/// <summary>
/// GitLab / self-managed GitLab のアダプタ(M4 §3/§15 ステップ8)。Webhook は X-Gitlab-Token による
/// トークン一致で検証する(GitHub の HMAC とは方式が異なる)。接続先はベース URL で切り替え、
/// self-managed に対応する。書き込み(ブランチ/MR 作成)は REST API v4 を叩くため本番専用。
/// </summary>
public class GitLabProvider : IGitProvider
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ISecretStore _secrets;

    public string Key => Models.GitProvider.GitLab;

    public GitLabProvider(IHttpClientFactory httpClientFactory, ISecretStore secrets)
    {
        _httpClientFactory = httpClientFactory;
        _secrets = secrets;
    }

    public bool VerifySignature(GitWebhookRequest request, string signingSecret)
    {
        if (!request.Headers.TryGetValue("x-gitlab-token", out var token) || string.IsNullOrEmpty(token))
        {
            return false;
        }

        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(token), Encoding.UTF8.GetBytes(signingSecret));
    }

    public string? GetDeliveryId(GitWebhookRequest request) =>
        request.Headers.TryGetValue("x-gitlab-event-uuid", out var id) && !string.IsNullOrWhiteSpace(id) ? id : null;

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

        var kind = GetString(root, "object_kind");
        return kind switch
        {
            "push" => ParsePush(root),
            "merge_request" => ParseMergeRequest(root),
            _ => null,
        };
    }

    private static GitEvent? ParsePush(JsonElement root)
    {
        if (!TryRepo(root, out var repo))
        {
            return null;
        }

        var refName = GetString(root, "ref"); // refs/heads/feature/x
        var branch = StripRefs(refName);
        var before = GetString(root, "before");
        var isCreate = before is not null && before.All(c => c == '0');

        var hintText = new StringBuilder(branch);
        if (root.TryGetProperty("commits", out var commits) && commits.ValueKind == JsonValueKind.Array)
        {
            foreach (var c in commits.EnumerateArray())
            {
                hintText.Append(' ').Append(GetString(c, "message"));
            }
        }

        return new GitEvent(
            isCreate ? GitEventType.BranchCreated : GitEventType.Push,
            repo, Actor(root, "user_id", "user_username"),
            branch, branch, null, DateTimeOffset.UtcNow, GitTaskHints.Extract(hintText.ToString()));
    }

    private static GitEvent? ParseMergeRequest(JsonElement root)
    {
        if (!TryRepo(root, out var repo) || !root.TryGetProperty("object_attributes", out var attrs))
        {
            return null;
        }

        var action = GetString(attrs, "action");
        var type = action switch
        {
            "open" or "reopen" => GitEventType.MrOpened,
            "merge" => GitEventType.MrMerged,
            "close" => GitEventType.MrClosed,
            _ => GitEventType.Unknown,
        };
        if (type == GitEventType.Unknown)
        {
            return null;
        }

        var iid = GetNumberAsString(attrs, "iid");
        var title = GetString(attrs, "title");
        var description = GetString(attrs, "description");
        var sourceBranch = GetString(attrs, "source_branch");

        GitActor? actor = null;
        if (root.TryGetProperty("user", out var user))
        {
            actor = Actor(user, "id", "username");
        }

        return new GitEvent(
            type, repo, actor, iid, title, GetString(attrs, "url"), DateTimeOffset.UtcNow,
            GitTaskHints.Extract($"{sourceBranch} {title} {description}"));
    }

    // --- 書き込み(本番専用。REST API v4) ---

    public async Task<GitRef> CreateBranchAsync(RepositoryTarget repo, string branchName, string fromRef, CancellationToken ct = default)
    {
        var client = await ClientAsync(repo.Connection, ct);
        var project = Uri.EscapeDataString(repo.FullName);
        var url = $"/api/v4/projects/{project}/repository/branches?branch={Uri.EscapeDataString(branchName)}&ref={Uri.EscapeDataString(fromRef)}";
        using var res = await client.PostAsync(url, null, ct);
        res.EnsureSuccessStatusCode();
        var json = JsonDocument.Parse(await res.Content.ReadAsStringAsync(ct)).RootElement;
        return new GitRef(branchName, GetString(json.GetProperty("commit"), "id") ?? "", GetString(json, "web_url") ?? "");
    }

    public async Task<GitPullRequest> CreatePullRequestAsync(RepositoryTarget repo, CreatePrInput input, CancellationToken ct = default)
    {
        var client = await ClientAsync(repo.Connection, ct);
        var project = Uri.EscapeDataString(repo.FullName);
        using var res = await client.PostAsJsonAsync($"/api/v4/projects/{project}/merge_requests", new
        {
            source_branch = input.SourceBranch,
            target_branch = input.TargetBranch,
            title = input.Title,
            description = input.Body,
        }, ct);
        res.EnsureSuccessStatusCode();
        var json = JsonDocument.Parse(await res.Content.ReadAsStringAsync(ct)).RootElement;
        return new GitPullRequest(
            GetNumberAsString(json, "iid") ?? "", GetString(json, "web_url") ?? "",
            GetString(json, "title") ?? input.Title, GitLinkStateFromGitLab(GetString(json, "state")));
    }

    public async Task<IReadOnlyList<GitRepository>> ListRepositoriesAsync(GitConnectionRef connection, CancellationToken ct = default)
    {
        var client = await ClientAsync(connection, ct);
        using var res = await client.GetAsync("/api/v4/projects?membership=true&per_page=100", ct);
        res.EnsureSuccessStatusCode();
        var json = JsonDocument.Parse(await res.Content.ReadAsStringAsync(ct)).RootElement;
        return json.EnumerateArray()
            .Select(p => new GitRepository(
                GetNumberAsString(p, "id") ?? "", GetString(p, "path_with_namespace") ?? "", GetString(p, "default_branch")))
            .ToList();
    }

    private async Task<HttpClient> ClientAsync(GitConnectionRef connection, CancellationToken ct)
    {
        var baseUrl = string.IsNullOrWhiteSpace(connection.BaseUrl) ? "https://gitlab.com" : connection.BaseUrl.TrimEnd('/');
        if (!SsrfGuard.IsAllowedBaseUrl(baseUrl))
        {
            throw new InvalidOperationException("接続先の URL が許可されていません。");
        }

        var token = await _secrets.GetSecretAsync(connection.SecretRef, ct)
            ?? throw new InvalidOperationException("GitLab のトークンを解決できません。");

        var client = _httpClientFactory.CreateClient();
        client.BaseAddress = new Uri(baseUrl);
        client.DefaultRequestHeaders.Add("PRIVATE-TOKEN", token);
        client.DefaultRequestHeaders.Add("User-Agent", "task-management-system");
        return client;
    }

    private static bool TryRepo(JsonElement root, out RepositoryRef repo)
    {
        repo = default!;
        if (!root.TryGetProperty("project", out var project))
        {
            return false;
        }

        var id = GetNumberAsString(project, "id");
        var fullName = GetString(project, "path_with_namespace");
        if (id is null || fullName is null)
        {
            return false;
        }

        repo = new RepositoryRef(id, fullName);
        return true;
    }

    private static GitActor? Actor(JsonElement root, string idProp, string nameProp)
    {
        var id = GetNumberAsString(root, idProp) ?? GetString(root, idProp);
        return id is null ? null : new GitActor(id, GetString(root, nameProp));
    }

    private static string StripRefs(string? refName) =>
        refName is null ? "" : refName.Replace("refs/heads/", "");

    private static string GitLinkStateFromGitLab(string? state) => state switch
    {
        "merged" => Models.GitLinkState.Merged,
        "closed" => Models.GitLinkState.Closed,
        _ => Models.GitLinkState.Open,
    };

    private static string? GetString(JsonElement root, string name) =>
        root.TryGetProperty(name, out var el) && el.ValueKind == JsonValueKind.String ? el.GetString() : null;

    private static string? GetNumberAsString(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var el))
        {
            return null;
        }

        return el.ValueKind switch
        {
            JsonValueKind.Number => el.GetRawText(),
            JsonValueKind.String => el.GetString(),
            _ => null,
        };
    }
}
