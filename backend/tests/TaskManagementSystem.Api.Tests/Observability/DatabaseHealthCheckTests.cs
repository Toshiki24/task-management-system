using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using TaskManagementSystem.Api.Data;
using TaskManagementSystem.Api.Observability;
using TaskManagementSystem.Api.Tests.Infrastructure;

namespace TaskManagementSystem.Api.Tests.Observability;

public class DatabaseHealthCheckTests : IClassFixture<TestDatabaseFixture>
{
    private readonly TestDatabaseFixture _db;

    public DatabaseHealthCheckTests(TestDatabaseFixture db)
    {
        _db = db;
    }

    [Fact(DisplayName = "接続でき、未適用マイグレーションが無ければ Healthy")]
    public async Task ReturnsHealthy_WhenConnectedAndMigrated()
    {
        await using var ctx = _db.CreateContext();
        var check = new DatabaseHealthCheck(ctx);

        var result = await check.CheckHealthAsync(new HealthCheckContext());

        Assert.Equal(HealthStatus.Healthy, result.Status);
        Assert.Equal(0, Assert.IsType<int>(result.Data["pendingMigrations"]));
    }

    [Fact(DisplayName = "DB に接続できなければ Unhealthy(例外を外部に漏らさない)")]
    public async Task ReturnsUnhealthy_WhenCannotConnect()
    {
        // 接続できないホスト/ポートを指す使い捨てコンテキスト
        await using var badCtx = new AppDbContext(
            new DbContextOptionsBuilder<AppDbContext>()
                .UseNpgsql("Host=127.0.0.1;Port=1;Database=none;Username=x;Password=y;Timeout=2")
                .UseSnakeCaseNamingConvention()
                .Options);
        var check = new DatabaseHealthCheck(badCtx);

        var result = await check.CheckHealthAsync(new HealthCheckContext());

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
    }
}
