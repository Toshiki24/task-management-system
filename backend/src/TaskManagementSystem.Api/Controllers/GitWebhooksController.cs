using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaskManagementSystem.Api.Dtos.Common;
using TaskManagementSystem.Api.Services;
using TaskManagementSystem.Api.Services.Git;

namespace TaskManagementSystem.Api.Controllers;

/// <summary>
/// Git プロバイダからの Webhook 受信口(M4 §5)。外部から直接呼ばれるため BFF を経由せず、
/// 認証クッキーも使わない。正当性は署名検証で担保する(クッキーが無いため CSRF の対象外)。
/// </summary>
[ApiController]
[AllowAnonymous]
[Route("api/git/webhooks/{provider}")]
public class GitWebhooksController : ControllerBase
{
    private readonly IWebhookService _service;

    public GitWebhooksController(IWebhookService service)
    {
        _service = service;
    }

    [HttpPost]
    public async Task<IActionResult> Receive(string provider)
    {
        // 署名検証には生のボディが必要なため、モデルバインドせずそのまま読む
        string rawBody;
        using (var reader = new StreamReader(Request.Body))
        {
            rawBody = await reader.ReadToEndAsync();
        }

        var headers = Request.Headers.ToDictionary(
            h => h.Key.ToLowerInvariant(), h => h.Value.ToString(), StringComparer.OrdinalIgnoreCase);

        var outcome = await _service.IngestAsync(provider, new GitWebhookRequest(headers, rawBody));

        return outcome.Result switch
        {
            WebhookIngestResult.UnknownProvider =>
                NotFound(new ErrorResponse("対応していないプロバイダです。")),
            WebhookIngestResult.MissingDeliveryId =>
                BadRequest(new ErrorResponse("配信 ID がありません。")),
            WebhookIngestResult.InvalidSignature =>
                Unauthorized(new ErrorResponse("署名の検証に失敗しました。")),
            // 重複は正常終了として 200(再送に対して安全に応答する)
            WebhookIngestResult.Duplicate => Ok(new { status = "duplicate" }),
            // 対象外(連携なし/未対応イベント)も再送させないため 202 で受理
            WebhookIngestResult.Ignored => Accepted(new { status = "ignored" }),
            _ => Accepted(new { status = "accepted" }),
        };
    }
}
