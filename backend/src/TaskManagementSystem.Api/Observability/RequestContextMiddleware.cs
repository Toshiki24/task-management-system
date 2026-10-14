using System.Diagnostics;
using System.IdentityModel.Tokens.Jwt;

namespace TaskManagementSystem.Api.Observability;

/// <summary>
/// 構造化ログのための共通コンテキスト(M5 §9)。
/// リクエストごとに相関 ID(<c>X-Request-Id</c>)を付与し、ログスコープに積む。
/// 完了時にメソッド・パス・ステータス・所要時間・ユーザー ID を1行で記録する(メトリクスの一次情報)。
/// 秘密情報(トークン・本文・クエリ文字列)はログに出さない。
/// </summary>
public class RequestContextMiddleware
{
    public const string RequestIdHeader = "X-Request-Id";

    private readonly RequestDelegate _next;
    private readonly ILogger<RequestContextMiddleware> _logger;

    public RequestContextMiddleware(RequestDelegate next, ILogger<RequestContextMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // 呼び出し側が相関 ID を付けていれば引き継ぎ、無ければ生成する
        var requestId = context.Request.Headers[RequestIdHeader].ToString();
        if (string.IsNullOrWhiteSpace(requestId))
        {
            requestId = Guid.NewGuid().ToString("N");
        }

        context.Items[RequestIdHeader] = requestId;
        // 例外ハンドラーがヘッダーを消去しても確実に付くよう、送信直前に設定する
        context.Response.OnStarting(() =>
        {
            context.Response.Headers[RequestIdHeader] = requestId;
            return Task.CompletedTask;
        });

        var stopwatch = Stopwatch.StartNew();
        using (_logger.BeginScope(new Dictionary<string, object> { ["RequestId"] = requestId }))
        {
            try
            {
                await _next(context);
            }
            finally
            {
                stopwatch.Stop();
                // 認証後なのでユーザーを特定できる(未認証は "-")。パスのみ記録しクエリ文字列は出さない
                var userId = context.User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value ?? "-";
                _logger.LogInformation(
                    "HTTP {Method} {Path} responded {StatusCode} in {ElapsedMs}ms (request {RequestId}, user {UserId})",
                    context.Request.Method,
                    context.Request.Path.Value,
                    context.Response.StatusCode,
                    stopwatch.ElapsedMilliseconds,
                    requestId,
                    userId);
            }
        }
    }
}
