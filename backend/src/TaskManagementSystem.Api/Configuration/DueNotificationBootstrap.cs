using System.Text.Json;
using Amazon.Lambda.Core;
using Amazon.Lambda.RuntimeSupport;
using Amazon.Lambda.Serialization.SystemTextJson;
using Microsoft.EntityFrameworkCore;
using TaskManagementSystem.Api.Data;
using TaskManagementSystem.Api.Services;

namespace TaskManagementSystem.Api.Configuration;

/// <summary>
/// 期限通知ジョブの起動処理(M3 §7)。環境変数 <c>JOB_MODE=due-notifications</c> のときに、
/// 通常の API ホストの代わりにこの処理を実行する(マイグレーション用と同じく同一コンテナイメージを使う)。
/// EventBridge のスケジュール(日次)から Lambda を起動する想定。
/// </summary>
public static class DueNotificationBootstrap
{
    public const string JobModeEnvironmentVariable = "JOB_MODE";
    public const string DueNotificationJob = "due-notifications";

    public static bool IsDueNotificationJob =>
        Environment.GetEnvironmentVariable(JobModeEnvironmentVariable) == DueNotificationJob;

    public static async Task RunAsync(IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection が設定されていません。");
        var withinDays = configuration.GetValue("Notifications:DueSoonWithinDays", 3);

        var handler = async (JsonElement _, ILambdaContext context) =>
        {
            await using var dbContext = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
                .UseNpgsql(connectionString)
                .UseSnakeCaseNamingConvention()
                .Options);

            var today = DateOnly.FromDateTime(JstNow());
            var service = new DueNotificationService(dbContext);
            var created = await service.GenerateAsync(today, withinDays);

            context.Logger.LogInformation($"期限通知ジョブ完了: 基準日={today:yyyy-MM-dd} 生成={created} 件");
            return $"due notifications created: {created}";
        };

        using var wrapper = HandlerWrapper.GetHandlerWrapper(handler, new DefaultLambdaJsonSerializer());
        using var bootstrap = new LambdaBootstrap(wrapper);
        await bootstrap.RunAsync();
    }

    // 期限は日本のカレンダーで判定する(利用者の体感に合わせる)
    private static DateTime JstNow()
    {
        try
        {
            var jst = TimeZoneInfo.FindSystemTimeZoneById("Asia/Tokyo");
            return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, jst);
        }
        catch (TimeZoneNotFoundException)
        {
            return DateTime.UtcNow.AddHours(9);
        }
    }
}
