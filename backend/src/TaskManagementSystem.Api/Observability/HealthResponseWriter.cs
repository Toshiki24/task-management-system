using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace TaskManagementSystem.Api.Observability;

/// <summary>
/// ヘルスチェックの JSON 応答(M5 §9)。全体状態と各チェックの状態を返す。
/// 例外やスタックトレースなどの内部情報は出さず、状態・説明・安全な付随情報(件数)のみ。
/// </summary>
public static class HealthResponseWriter
{
    public static Task WriteAsync(HttpContext context, HealthReport report)
    {
        context.Response.ContentType = "application/json";

        var payload = new
        {
            status = MapStatus(report.Status),
            checks = report.Entries.ToDictionary(
                entry => entry.Key,
                entry => new
                {
                    status = MapStatus(entry.Value.Status),
                    description = entry.Value.Description,
                    data = entry.Value.Data.Count > 0 ? entry.Value.Data : null,
                }),
        };

        return context.Response.WriteAsync(JsonSerializer.Serialize(payload));
    }

    // 既存の死活監視との互換のため、正常時は "ok" を使う
    private static string MapStatus(HealthStatus status) => status switch
    {
        HealthStatus.Healthy => "ok",
        HealthStatus.Degraded => "degraded",
        _ => "unhealthy",
    };
}
