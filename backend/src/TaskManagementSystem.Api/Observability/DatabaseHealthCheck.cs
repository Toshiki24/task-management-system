using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using TaskManagementSystem.Api.Data;

namespace TaskManagementSystem.Api.Observability;

/// <summary>
/// ヘルスチェック(M5 §9)の DB 確認。接続可否と未適用マイグレーションの有無を見る。
/// 秘密情報は返さない(件数と可否のみ)。
/// </summary>
public class DatabaseHealthCheck : IHealthCheck
{
    private readonly AppDbContext _dbContext;

    public DatabaseHealthCheck(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            if (!await _dbContext.Database.CanConnectAsync(cancellationToken))
            {
                return HealthCheckResult.Unhealthy("データベースに接続できません。");
            }

            var pending = (await _dbContext.Database.GetPendingMigrationsAsync(cancellationToken)).Count();
            var data = new Dictionary<string, object> { ["pendingMigrations"] = pending };

            return pending > 0
                ? HealthCheckResult.Degraded($"未適用のマイグレーションが {pending} 件あります。", data: data)
                : HealthCheckResult.Healthy("ok", data);
        }
        catch (Exception ex)
        {
            // 例外メッセージはそのまま返さず、固定文言にする(内部情報の漏えい防止)
            return HealthCheckResult.Unhealthy("データベースの確認に失敗しました。", ex);
        }
    }
}
