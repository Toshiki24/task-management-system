namespace TaskManagementSystem.Api.Services.Git;

/// <summary>受信した Webhook リクエスト(ヘッダ＋生ボディ)。署名検証に生のボディが必要。</summary>
public record GitWebhookRequest(IReadOnlyDictionary<string, string> Headers, string RawBody);

/// <summary>正規化された Git イベントの種別(Phase 2 M4 §3)。</summary>
public enum GitEventType
{
    Unknown,
    BranchCreated,
    PrOpened,
    PrMerged,
    PrClosed,
    MrOpened,
    MrMerged,
    MrClosed,
    Push,
    PipelineSucceeded,
    PipelineFailed,
}

/// <summary>プロバイダ側のリポジトリ参照。</summary>
public record RepositoryRef(string ExternalRepoId, string FullName);

/// <summary>プロバイダ側のユーザー(actor)。</summary>
public record GitActor(string ExternalUserId, string? Username);

/// <summary>
/// 各プロバイダの Webhook を共通化したイベント(Phase 2 M4 §3)。遷移エンジンとリンク処理は
/// この型だけを見るため、GitHub/GitLab の差異に依存しない。
/// </summary>
public record GitEvent(
    GitEventType Type,
    RepositoryRef Repo,
    GitActor? Actor,
    string? Ref,
    string? Title,
    string? Url,
    DateTimeOffset OccurredAt,
    IReadOnlyList<string> TaskHints);

/// <summary>接続の参照(資格情報の解決に使う)。</summary>
public record GitConnectionRef(long ConnectionId, string? BaseUrl, string? SecretRef);

/// <summary>ブランチ/PR 作成に使うリポジトリ指定。</summary>
public record RepositoryTarget(GitConnectionRef Connection, string ExternalRepoId, string FullName);

public record GitRef(string Name, string Sha, string Url);

public record CreatePrInput(string SourceBranch, string TargetBranch, string Title, string? Body);

public record GitPullRequest(string Number, string Url, string Title, string State);

public record GitRepository(string ExternalRepoId, string FullName, string? DefaultBranch);

/// <summary>
/// Git プロバイダ抽象(Phase 2 M4 §3)。GitHub / GitLab / self-managed の差異
/// (署名方式・API・認証)をアダプタで吸収する。テストでは <see cref="FakeGitProvider"/> で置き換える。
/// </summary>
public interface IGitProvider
{
    /// <summary>プロバイダ識別子(GITHUB/GITLAB)。</summary>
    string Key { get; }

    /// <summary>Webhook の署名を検証する(方式はプロバイダごとに異なる)。</summary>
    bool VerifySignature(GitWebhookRequest request, string signingSecret);

    /// <summary>Webhook の配信 ID(冪等キー)を取り出す。取得できなければ null。</summary>
    string? GetDeliveryId(GitWebhookRequest request);

    /// <summary>Webhook ペイロードを共通イベントへ正規化する。対象外イベントは null。</summary>
    GitEvent? ParseEvent(GitWebhookRequest request);

    Task<GitRef> CreateBranchAsync(RepositoryTarget repo, string branchName, string fromRef, CancellationToken ct = default);
    Task<GitPullRequest> CreatePullRequestAsync(RepositoryTarget repo, CreatePrInput input, CancellationToken ct = default);
    Task<IReadOnlyList<GitRepository>> ListRepositoriesAsync(GitConnectionRef connection, CancellationToken ct = default);
}

/// <summary>プロバイダ識別子から実装を解決する(Phase 2 M4 §3)。</summary>
public interface IGitProviderResolver
{
    /// <summary>指定キーのプロバイダを返す。未対応なら null。</summary>
    IGitProvider? Resolve(string providerKey);
}
