using TaskManagementSystem.Api.Services.Git;

namespace TaskManagementSystem.Api.Services;

public enum WebhookIngestResult
{
    /// <summary>署名検証に成功し、新規に記録した。</summary>
    Accepted,
    /// <summary>同一配信 ID を受信済み(冪等によりスキップ)。</summary>
    Duplicate,
    /// <summary>対応していないプロバイダ。</summary>
    UnknownProvider,
    /// <summary>配信 ID(冪等キー)が無い。</summary>
    MissingDeliveryId,
    /// <summary>連携済みリポジトリに紐づかない等で対象外(無視)。</summary>
    Ignored,
    /// <summary>署名検証に失敗した。</summary>
    InvalidSignature,
}

public record WebhookIngestOutcome(WebhookIngestResult Result);

public interface IWebhookService
{
    /// <summary>受信した Webhook を検証し、冪等に記録する(M4 §5。遷移は後続ステップ)。</summary>
    Task<WebhookIngestOutcome> IngestAsync(string providerKey, GitWebhookRequest request, CancellationToken ct = default);
}
