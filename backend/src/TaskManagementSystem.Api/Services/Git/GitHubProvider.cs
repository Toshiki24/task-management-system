using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace TaskManagementSystem.Api.Services.Git;

/// <summary>
/// GitHub(github.com / GitHub Enterprise)のアダプタ(M4 §3/§15 ステップ8)。Webhook は
/// X-Hub-Signature-256(HMAC-SHA256)で検証する。書き込み(ブランチ/PR 作成)は REST API を
/// 叩くため本番専用。認証トークンはシークレットストアから解決する。
/// </summary>
public class GitHubProvider : IGitProvider
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ISecretStore _secrets;

    public string Key => Models.GitProvider.GitHub;

    public GitHubProvider(IHttpClientFactory httpClientFactory, ISecretStore secrets)
    {
        _httpClientFactory = httpClientFactory;
        _secrets = secrets;
    }

    public bool VerifySignature(GitWebhookRequest request, string signingSecret)
    {
        if (!request.Headers.TryGetValue("x-hub-signature-256", out var provided) || string.IsNullOrEmpty(provided))
        {
            return false;
        }

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(signingSecret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(request.RawBody));
        var expected = "sha256=" + Convert.ToHexString(hash).ToLowerInvariant();
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(provided), Encoding.UTF8.GetBytes(expected));
    }

    public string? GetDeliveryId(GitWebhookRequest request) =>
        request.Headers.TryGetValue("x-github-delivery", out var id) && !string.IsNullOrWhiteSpace(id) ? id : null;

    public GitEvent? ParseEvent(GitWebhookRequest request)
    {
        request.Headers.TryGetValue("x-github-event", out var eventName);

        JsonElement root;
        try
        {
            root = JsonDocument.Parse(request.RawBody).RootElement;
        }
        catch (JsonException)
        {
            return null;
        }

        return eventName switch
        {
            "create" => ParseCreate(root),
            "pull_request" => ParsePullRequest(root),
            "push" => ParsePush(root),
            _ => null,
        };
    }

    private static GitEvent? ParseCreate(JsonElement root)
    {
        if (GetString(root, "ref_type") != "branch" || !TryRepo(root, out var repo))
        {
            return null;
        }

        var branch = GetString(root, "ref");
        return new GitEvent(
            GitEventType.BranchCreated, repo, Sender(root), branch, branch, null,
            DateTimeOffset.UtcNow, GitTaskHints.Extract(branch));
    }

    private static GitEvent? ParsePullRequest(JsonElement root)
    {
        if (!TryRepo(root, out var repo) || !root.TryGetProperty("pull_request", out var pr))
        {
            return null;
        }

        var action = GetString(root, "action");
        var merged = pr.TryGetProperty("merged", out var m) && m.ValueKind == JsonValueKind.True;
        var type = action switch
        {
            "opened" or "reopened" => GitEventType.PrOpened,
            "closed" => merged ? GitEventType.PrMerged : GitEventType.PrClosed,
            _ => GitEventType.Unknown,
        };
        if (type == GitEventType.Unknown)
        {
            return null;
        }

        var number = GetNumberAsString(root, "number") ?? GetNumberAsString(pr, "number");
        var title = GetString(pr, "title");
        var body = GetString(pr, "body");
        var headRef = pr.TryGetProperty("head", out var head) ? GetString(head, "ref") : null;

        return new GitEvent(
            type, repo, Sender(root), number, title, GetString(pr, "html_url"), DateTimeOffset.UtcNow,
            GitTaskHints.Extract($"{headRef} {title} {body}"));
    }

    private static GitEvent? ParsePush(JsonElement root)
    {
        if (!TryRepo(root, out var repo))
        {
            return null;
        }

        var branch = (GetString(root, "ref") ?? "").Replace("refs/heads/", "");
        var created = root.TryGetProperty("created", out var c) && c.ValueKind == JsonValueKind.True;
        var hints = new StringBuilder(branch);
        if (root.TryGetProperty("commits", out var commits) && commits.ValueKind == JsonValueKind.Array)
        {
            foreach (var commit in commits.EnumerateArray())
            {
                hints.Append(' ').Append(GetString(commit, "message"));
            }
        }

        return new GitEvent(
            created ? GitEventType.BranchCreated : GitEventType.Push, repo, Sender(root),
            branch, branch, null, DateTimeOffset.UtcNow, GitTaskHints.Extract(hints.ToString()));
    }

    // --- 書き込み(本番専用。REST API) ---

    public async Task<GitRef> CreateBranchAsync(RepositoryTarget repo, string branchName, string fromRef, CancellationToken ct = default)
    {
        var client = await ClientAsync(repo.Connection, ct);
        // 派生元の SHA を取得してから ref を作る
        using var baseRes = await client.GetAsync($"/repos/{repo.FullName}/git/ref/heads/{fromRef}", ct);
        baseRes.EnsureSuccessStatusCode();
        var baseSha = JsonDocument.Parse(await baseRes.Content.ReadAsStringAsync(ct))
            .RootElement.GetProperty("object").GetProperty("sha").GetString();

        using var res = await client.PostAsJsonAsync($"/repos/{repo.FullName}/git/refs",
            new { @ref = $"refs/heads/{branchName}", sha = baseSha }, ct);
        res.EnsureSuccessStatusCode();
        var json = JsonDocument.Parse(await res.Content.ReadAsStringAsync(ct)).RootElement;
        return new GitRef(branchName, GetString(json.GetProperty("object"), "sha") ?? "", GetString(json, "url") ?? "");
    }

    public async Task<GitPullRequest> CreatePullRequestAsync(RepositoryTarget repo, CreatePrInput input, CancellationToken ct = default)
    {
        var client = await ClientAsync(repo.Connection, ct);
        using var res = await client.PostAsJsonAsync($"/repos/{repo.FullName}/pulls", new
        {
            title = input.Title,
            head = input.SourceBranch,
            @base = input.TargetBranch,
            body = input.Body,
        }, ct);
        res.EnsureSuccessStatusCode();
        var json = JsonDocument.Parse(await res.Content.ReadAsStringAsync(ct)).RootElement;
        return new GitPullRequest(
            GetNumberAsString(json, "number") ?? "", GetString(json, "html_url") ?? "",
            GetString(json, "title") ?? input.Title, Models.GitLinkState.Open);
    }

    public async Task<IReadOnlyList<GitRepository>> ListRepositoriesAsync(GitConnectionRef connection, CancellationToken ct = default)
    {
        var client = await ClientAsync(connection, ct);
        using var res = await client.GetAsync("/installation/repositories?per_page=100", ct);
        res.EnsureSuccessStatusCode();
        var json = JsonDocument.Parse(await res.Content.ReadAsStringAsync(ct)).RootElement;
        var repos = json.TryGetProperty("repositories", out var arr) ? arr : json;
        return repos.EnumerateArray()
            .Select(r => new GitRepository(
                GetNumberAsString(r, "id") ?? "", GetString(r, "full_name") ?? "", GetString(r, "default_branch")))
            .ToList();
    }

    private async Task<HttpClient> ClientAsync(GitConnectionRef connection, CancellationToken ct)
    {
        var baseUrl = string.IsNullOrWhiteSpace(connection.BaseUrl) ? "https://api.github.com" : connection.BaseUrl.TrimEnd('/');
        if (!SsrfGuard.IsAllowedBaseUrl(baseUrl))
        {
            throw new InvalidOperationException("接続先の URL が許可されていません。");
        }

        var token = await _secrets.GetSecretAsync(connection.SecretRef, ct)
            ?? throw new InvalidOperationException("GitHub のトークンを解決できません。");

        var client = _httpClientFactory.CreateClient();
        client.BaseAddress = new Uri(baseUrl);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        client.DefaultRequestHeaders.UserAgent.ParseAdd("task-management-system");
        return client;
    }

    private static bool TryRepo(JsonElement root, out RepositoryRef repo)
    {
        repo = default!;
        if (!root.TryGetProperty("repository", out var repository))
        {
            return false;
        }

        var id = GetNumberAsString(repository, "id");
        var fullName = GetString(repository, "full_name");
        if (id is null || fullName is null)
        {
            return false;
        }

        repo = new RepositoryRef(id, fullName);
        return true;
    }

    private static GitActor? Sender(JsonElement root)
    {
        if (!root.TryGetProperty("sender", out var sender))
        {
            return null;
        }

        var id = GetNumberAsString(sender, "id");
        return id is null ? null : new GitActor(id, GetString(sender, "login"));
    }

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
