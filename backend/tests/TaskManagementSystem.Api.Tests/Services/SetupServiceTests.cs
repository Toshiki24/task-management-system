using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using TaskManagementSystem.Api.Data;
using TaskManagementSystem.Api.Dtos.Setup;
using TaskManagementSystem.Api.Services;
using TaskManagementSystem.Api.Tests.Infrastructure;

namespace TaskManagementSystem.Api.Tests.Services;

public class SetupServiceTests : IClassFixture<TestDatabaseFixture>
{
    private readonly TestDatabaseFixture _db;

    public SetupServiceTests(TestDatabaseFixture db)
    {
        _db = db;
    }

    private SetupService CreateService(AppDbContext ctx, bool enabled = true)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["SELF_HOST_SETUP"] = enabled ? "true" : "false",
                ["ConnectionStrings:DefaultConnection"] = _db.ConnectionString,
            })
            .Build();
        return new SetupService(ctx, config, NullLogger<SetupService>.Instance);
    }

    /// <summary>このクラス専用DBの users を初期化する(件数判定が全体件数に依存するため)。</summary>
    private static async Task ResetUsersAsync(AppDbContext ctx)
    {
        await ctx.Database.ExecuteSqlRawAsync("TRUNCATE users RESTART IDENTITY CASCADE");
    }

    private static CreateInitialAdminRequest ValidRequest(string email = "admin@example.test") =>
        new("管理者", email, "setup-password-123456");

    [Fact(DisplayName = "状態: 0件なら要セットアップ、1件以上なら不要、無効なら Disabled")]
    public async Task GetStatus_ReflectsUserCountAndFlag()
    {
        await using var ctx = _db.CreateContext();
        await ResetUsersAsync(ctx);

        var enabled = CreateService(ctx);
        var empty = await enabled.GetStatusAsync();
        Assert.Equal(SetupStatusResult.Ok, empty.Result);
        Assert.True(empty.Data!.SetupRequired);

        await TestData.CreateUserAsync(ctx);
        var afterUser = await enabled.GetStatusAsync();
        Assert.Equal(SetupStatusResult.Ok, afterUser.Result);
        Assert.False(afterUser.Data!.SetupRequired);

        // 機能無効(AWS 本番相当)なら Disabled(→ controller 404)
        Assert.Equal(SetupStatusResult.Disabled, (await CreateService(ctx, enabled: false).GetStatusAsync()).Result);
    }

    [Fact(DisplayName = "正常作成: System Admin が作られ、パスワードは BCrypt でハッシュ化される")]
    public async Task CreateInitialAdmin_CreatesHashedSystemAdmin()
    {
        await using var ctx = _db.CreateContext();
        await ResetUsersAsync(ctx);
        var request = ValidRequest();

        var result = await CreateService(ctx).CreateInitialAdminAsync(request);
        Assert.Equal(CreateInitialAdminResult.Success, result);

        await using var assert = _db.CreateContext();
        var user = await assert.Users.SingleAsync(u => u.Email == request.Email);
        Assert.True(user.IsSystemAdmin);
        Assert.NotEqual(request.Password, user.PasswordHash); // 平文を保存しない
        Assert.StartsWith("$2", user.PasswordHash); // BCrypt 形式
        Assert.True(BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash));
    }

    [Fact(DisplayName = "機能無効なら作成しない(Disabled)")]
    public async Task CreateInitialAdmin_DisabledDoesNothing()
    {
        await using var ctx = _db.CreateContext();
        await ResetUsersAsync(ctx);

        var result = await CreateService(ctx, enabled: false).CreateInitialAdminAsync(ValidRequest());
        Assert.Equal(CreateInitialAdminResult.Disabled, result);

        await using var assert = _db.CreateContext();
        Assert.False(await assert.Users.AnyAsync());
    }

    [Fact(DisplayName = "既にユーザーが居れば AlreadyInitialized で拒否する")]
    public async Task CreateInitialAdmin_RejectsWhenUsersExist()
    {
        await using var ctx = _db.CreateContext();
        await ResetUsersAsync(ctx);
        await TestData.CreateUserAsync(ctx);

        var result = await CreateService(ctx).CreateInitialAdminAsync(ValidRequest("another@example.test"));
        Assert.Equal(CreateInitialAdminResult.AlreadyInitialized, result);
    }

    [Fact(DisplayName = "同時リクエストでも管理者は1件しか作られない")]
    public async Task CreateInitialAdmin_ConcurrentCreatesOnlyOne()
    {
        await using var seedCtx = _db.CreateContext();
        await ResetUsersAsync(seedCtx);

        // それぞれ独立した DbContext(スレッド安全でないため)で同時に作成を試みる
        await using var ctx1 = _db.CreateContext();
        await using var ctx2 = _db.CreateContext();
        var task1 = CreateService(ctx1).CreateInitialAdminAsync(ValidRequest("admin1@example.test"));
        var task2 = CreateService(ctx2).CreateInitialAdminAsync(ValidRequest("admin2@example.test"));
        var results = await Task.WhenAll(task1, task2);

        // 片方だけ成功し、もう片方は競合として弾かれる
        Assert.Equal(1, results.Count(r => r == CreateInitialAdminResult.Success));
        Assert.Equal(1, results.Count(r => r == CreateInitialAdminResult.AlreadyInitialized));

        await using var assert = _db.CreateContext();
        Assert.Equal(1, await assert.Users.CountAsync());
    }
}
