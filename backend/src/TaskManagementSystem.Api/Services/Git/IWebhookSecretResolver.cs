using TaskManagementSystem.Api.Models;

namespace TaskManagementSystem.Api.Services.Git;

/// <summary>
/// Git 接続の Webhook 署名シークレットを解決する(M4 §4/§5)。
/// 資格情報は DB に平文で置かないため、接続の <see cref="GitConnection.SecretRef"/> を
/// シークレットストアの参照として解決する。
/// </summary>
public interface IWebhookSecretResolver
{
    /// <summary>接続の署名シークレットを返す。解決できなければ null。</summary>
    Task<string?> ResolveSigningSecretAsync(GitConnection connection, CancellationToken ct = default);
}

/// <summary>
/// 開発・テスト用のシークレット解決(M4 §5)。secret_ref をそのまま署名シークレットとして扱う。
/// 本番では Secrets Manager から取得する実装(§15 ステップ7)に差し替える。
/// </summary>
public class DevWebhookSecretResolver : IWebhookSecretResolver
{
    public Task<string?> ResolveSigningSecretAsync(GitConnection connection, CancellationToken ct = default) =>
        Task.FromResult(string.IsNullOrWhiteSpace(connection.SecretRef) ? null : connection.SecretRef);
}
