using Microsoft.EntityFrameworkCore;
using Npgsql;
using TaskManagementSystem.Api.Data;
using TaskManagementSystem.Api.Models;
using TaskManagementSystem.Api.Services.Git;

namespace TaskManagementSystem.Api.Services;

/// <summary>
/// Git Webhook の受信基盤(M4 §5)。署名検証・冪等記録を担う。公開エンドポイントから呼ばれ、
/// 認証はクッキーではなく署名で行う。遷移・リンク生成は後続ステップ(§15 ステップ4/5)。
/// すべて LINQ/EF。
/// </summary>
public class WebhookService : IWebhookService
{
    private readonly AppDbContext _dbContext;
    private readonly IGitProviderResolver _providers;
    private readonly IWebhookSecretResolver _secrets;
    private readonly IGitLinkService _gitLinks;

    public WebhookService(
        AppDbContext dbContext, IGitProviderResolver providers, IWebhookSecretResolver secrets, IGitLinkService gitLinks)
    {
        _dbContext = dbContext;
        _providers = providers;
        _secrets = secrets;
        _gitLinks = gitLinks;
    }

    public async Task<WebhookIngestOutcome> IngestAsync(
        string providerKey, GitWebhookRequest request, CancellationToken ct = default)
    {
        var provider = _providers.Resolve(providerKey);
        if (provider is null)
        {
            return new WebhookIngestOutcome(WebhookIngestResult.UnknownProvider);
        }

        // 冪等キー(配信 ID)が無いと重複排除できないため必須
        var deliveryId = provider.GetDeliveryId(request);
        if (string.IsNullOrWhiteSpace(deliveryId))
        {
            return new WebhookIngestOutcome(WebhookIngestResult.MissingDeliveryId);
        }

        // ペイロードを正規化(この時点では未検証。署名検証の前に副作用は起こさない)
        var gitEvent = provider.ParseEvent(request);
        if (gitEvent is null)
        {
            // 対応していないイベント種別は無視(記録しない)
            return new WebhookIngestOutcome(WebhookIngestResult.Ignored);
        }

        // 対象の連携リポジトリを特定する(署名検証はその接続の秘密で行う)
        var link = await _dbContext.RepositoryLinks
            .Where(r => r.ExternalRepoId == gitEvent.Repo.ExternalRepoId
                && r.GitConnection.Provider == provider.Key
                && r.GitConnection.Status == GitConnectionStatus.Active)
            .Select(r => new { RepositoryLinkId = r.Id, r.ProjectId, Connection = r.GitConnection })
            .FirstOrDefaultAsync(ct);
        if (link is null)
        {
            // 連携されていないリポジトリの Webhook は対象外(記録しない)
            return new WebhookIngestOutcome(WebhookIngestResult.Ignored);
        }

        var connection = link.Connection;
        var secret = await _secrets.ResolveSigningSecretAsync(connection, ct);
        var verified = !string.IsNullOrEmpty(secret) && provider.VerifySignature(request, secret);

        // 冪等: 同一接続・同一配信 ID は 1 件に抑える。先に存在を確認する
        var existing = await _dbContext.WebhookEvents
            .AnyAsync(e => e.GitConnectionId == connection.Id && e.ExternalEventId == deliveryId, ct);
        if (existing)
        {
            return new WebhookIngestOutcome(WebhookIngestResult.Duplicate);
        }

        var record = new WebhookEvent
        {
            GitConnectionId = connection.Id,
            Provider = provider.Key,
            ExternalEventId = deliveryId,
            EventType = gitEvent.Type.ToString(),
            SignatureVerified = verified,
            // 署名 NG は FAILED、OK は RECEIVED(遷移処理は後続ステップで PROCESSED にする)
            Status = verified ? WebhookEventStatus.Received : WebhookEventStatus.Failed,
            // received_at は DB 既定(CURRENT_TIMESTAMP)に任せる
        };

        try
        {
            _dbContext.WebhookEvents.Add(record);
            await _dbContext.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            // 競合で冪等キーの一意制約に触れた場合は重複として扱う(再送耐性)
            return new WebhookIngestOutcome(WebhookIngestResult.Duplicate);
        }

        if (!verified)
        {
            return new WebhookIngestOutcome(WebhookIngestResult.InvalidSignature);
        }

        // 署名検証済みの新規イベントを取り込む(task_git_links 作成・タイムライン。遷移は後続ステップ)
        await _gitLinks.ApplyEventAsync(
            new GitLinkContext(link.RepositoryLinkId, link.ProjectId, connection.WorkspaceId, provider.Key), gitEvent, ct);

        record.Status = WebhookEventStatus.Processed;
        record.ProcessedAt = DateTime.Now;
        await _dbContext.SaveChangesAsync(ct);

        return new WebhookIngestOutcome(WebhookIngestResult.Accepted);
    }

    /// <summary>一意制約違反(冪等キーの競合)かどうか。PostgreSQL のエラーコード 23505。</summary>
    private static bool IsUniqueViolation(DbUpdateException ex) =>
        ex.InnerException is Npgsql.PostgresException { SqlState: "23505" };
}
