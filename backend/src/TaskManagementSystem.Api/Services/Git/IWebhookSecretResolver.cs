using TaskManagementSystem.Api.Models;

namespace TaskManagementSystem.Api.Services.Git;

/// <summary>
/// シークレットストアの抽象(M4 §4)。資格情報は DB に平文で置かず、参照(secret_ref)から解決する。
/// 本番は Secrets Manager の値を起動時に IConfiguration へ展開し、ここから引く(§15 ステップ7)。
/// </summary>
public interface ISecretStore
{
    /// <summary>参照(secret_ref)から秘密値を解決する。解決できなければ null。</summary>
    Task<string?> GetSecretAsync(string? secretRef, CancellationToken ct = default);
}

/// <summary>
/// IConfiguration 経由のシークレットストア(M4 §4)。Secrets Manager から展開された
/// <c>Git:Secrets:{secret_ref}</c> を読む。開発・テストでは未設定のため、参照そのものを
/// 秘密として扱う(<paramref name="allowRefAsSecret"/> が true のときのみ)。
/// </summary>
public class ConfigSecretStore : ISecretStore
{
    private readonly IConfiguration _configuration;
    private readonly bool _allowRefAsSecret;

    public ConfigSecretStore(IConfiguration configuration, bool allowRefAsSecret)
    {
        _configuration = configuration;
        _allowRefAsSecret = allowRefAsSecret;
    }

    public Task<string?> GetSecretAsync(string? secretRef, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(secretRef))
        {
            return Task.FromResult<string?>(null);
        }

        var fromConfig = _configuration[$"Git:Secrets:{secretRef}"];
        if (!string.IsNullOrEmpty(fromConfig))
        {
            return Task.FromResult<string?>(fromConfig);
        }

        // 本番で未設定なら null(署名検証を安全に失敗させる)。開発・テストは参照を秘密として扱う
        return Task.FromResult<string?>(_allowRefAsSecret ? secretRef : null);
    }
}

/// <summary>
/// Git 接続の Webhook 署名シークレットを解決する(M4 §5)。接続の <see cref="GitConnection.SecretRef"/> を
/// <see cref="ISecretStore"/> 経由で実際の秘密値に解決する。
/// </summary>
public interface IWebhookSecretResolver
{
    Task<string?> ResolveSigningSecretAsync(GitConnection connection, CancellationToken ct = default);
}

/// <summary>シークレットストアに委譲して署名シークレットを解決する。</summary>
public class SecretStoreWebhookSecretResolver : IWebhookSecretResolver
{
    private readonly ISecretStore _secrets;

    public SecretStoreWebhookSecretResolver(ISecretStore secrets)
    {
        _secrets = secrets;
    }

    public Task<string?> ResolveSigningSecretAsync(GitConnection connection, CancellationToken ct = default) =>
        _secrets.GetSecretAsync(connection.SecretRef, ct);
}
