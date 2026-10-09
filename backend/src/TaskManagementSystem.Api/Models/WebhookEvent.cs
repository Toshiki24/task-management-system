namespace TaskManagementSystem.Api.Models;

/// <summary>受信 Webhook の処理状態(Phase 2 M4 §5)。</summary>
public static class WebhookEventStatus
{
    public const string Received = "RECEIVED";
    public const string Processed = "PROCESSED";
    /// <summary>冪等により重複として処理をスキップ。</summary>
    public const string Skipped = "SKIPPED";
    public const string Failed = "FAILED";

    public static readonly string[] All = { Received, Processed, Skipped, Failed };
}

/// <summary>
/// 受信した Webhook の記録(Phase 2 M4 §5)。署名検証結果・冪等キー・処理状態を残し、
/// 再送(同一配信 ID)の重複処理を防ぎ、監査にも使う。ペイロード本体は保持しない。
/// </summary>
public class WebhookEvent
{
    public long Id { get; set; }
    public long GitConnectionId { get; set; }

    public string Provider { get; set; } = null!;

    /// <summary>プロバイダの配信 ID(冪等キー)。(git_connection_id, external_event_id) で一意。</summary>
    public string ExternalEventId { get; set; } = null!;

    public string EventType { get; set; } = null!;
    public bool SignatureVerified { get; set; }
    public string Status { get; set; } = WebhookEventStatus.Received;

    public DateTime ReceivedAt { get; set; }
    public DateTime? ProcessedAt { get; set; }

    public GitConnection GitConnection { get; set; } = null!;
}
