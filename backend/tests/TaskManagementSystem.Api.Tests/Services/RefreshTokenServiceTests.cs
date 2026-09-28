using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using TaskManagementSystem.Api.Models;
using TaskManagementSystem.Api.Services;
using TaskManagementSystem.Api.Tests.Infrastructure;

namespace TaskManagementSystem.Api.Tests.Services;

public class RefreshTokenServiceTests : IClassFixture<TestDatabaseFixture>
{
    private static readonly TimeSpan Lifetime = TimeSpan.FromDays(7);
    private static readonly TimeSpan ReuseGracePeriod = TimeSpan.FromSeconds(10);

    private readonly TestDatabaseFixture _db;
    private readonly ManualTimeProvider _time = new();

    public RefreshTokenServiceTests(TestDatabaseFixture db)
    {
        _db = db;
    }

    internal static IConfiguration CreateConfiguration() =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["RefreshToken:ExpiresDays"] = Lifetime.TotalDays.ToString(),
                ["RefreshToken:ReuseGraceSeconds"] = ReuseGracePeriod.TotalSeconds.ToString(),
            })
            .Build();

    private RefreshTokenService CreateService(Api.Data.AppDbContext context) => new(context, _time, CreateConfiguration());

    private async Task<(User User, string Token)> IssueAsync()
    {
        await using var arrange = _db.CreateContext();
        var user = await TestData.CreateUserAsync(arrange);
        await using var context = _db.CreateContext();
        return (user, await CreateService(context).IssueAsync(user.Id));
    }

    private async Task<RotatedRefreshToken?> RotateAsync(string token)
    {
        await using var context = _db.CreateContext();
        return await CreateService(context).RotateAsync(token);
    }

    private async Task<List<RefreshToken>> TokensOfAsync(long userId)
    {
        await using var assert = _db.CreateContext();
        return await assert.RefreshTokens.Where(t => t.UserId == userId).OrderBy(t => t.Id).ToListAsync();
    }

    [Fact(DisplayName = "UT-901 トークンはハッシュ値で保存され、平文は保存されない")]
    public async Task IssueAsync_StoresOnlyHash()
    {
        var (user, token) = await IssueAsync();

        var stored = Assert.Single(await TokensOfAsync(user.Id));
        var expectedHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token))).ToLowerInvariant();
        Assert.Equal(expectedHash, stored.TokenHash);
        Assert.NotEqual(token, stored.TokenHash);
        Assert.True(token.Length >= 43, "32バイト以上の乱数から生成されていること");
        Assert.Equal(_time.GetUtcNow().UtcDateTime + Lifetime, DateTime.SpecifyKind(stored.ExpiresAt, DateTimeKind.Utc));
    }

    [Fact(DisplayName = "UT-902 置き換えると新しいトークンが発行され、元のトークンは失効する")]
    public async Task RotateAsync_IssuesNewToken_AndRevokesOldToken()
    {
        var (user, token) = await IssueAsync();

        var rotated = await RotateAsync(token);

        Assert.NotNull(rotated);
        Assert.Equal(user.Id, rotated.User.Id);
        Assert.NotEqual(token, rotated.NewRefreshToken);
        var tokens = await TokensOfAsync(user.Id);
        Assert.Equal(2, tokens.Count);
        Assert.Equal(RefreshTokenRevokedReason.Rotated, tokens[0].RevokedReason);
        Assert.Null(tokens[1].RevokedAt);
    }

    [Fact(DisplayName = "UT-903 猶予期間内なら、置き換え済みのトークンでも再発行できる")]
    public async Task RotateAsync_AllowsReuse_WithinGracePeriod()
    {
        var (user, token) = await IssueAsync();
        var first = await RotateAsync(token);

        // 同時に送られたリクエストが、同じトークンで少し遅れて置き換えを行うケース
        _time.Advance(ReuseGracePeriod - TimeSpan.FromSeconds(1));
        var second = await RotateAsync(token);

        Assert.NotNull(second);
        Assert.NotEqual(first!.NewRefreshToken, second.NewRefreshToken);
        // どちらの新しいトークンも有効なまま
        Assert.NotNull(await RotateAsync(first.NewRefreshToken));
        Assert.NotNull(await RotateAsync(second.NewRefreshToken));
        Assert.DoesNotContain(await TokensOfAsync(user.Id), t => t.RevokedReason == RefreshTokenRevokedReason.ReuseDetected);
    }

    [Fact(DisplayName = "UT-904 猶予期間を過ぎて置き換え済みのトークンが使われると、ユーザーの全トークンが失効する")]
    public async Task RotateAsync_RevokesAllTokens_WhenReusedAfterGracePeriod()
    {
        var (user, token) = await IssueAsync();
        var rotated = await RotateAsync(token);

        _time.Advance(ReuseGracePeriod);
        var reused = await RotateAsync(token);

        Assert.Null(reused);
        // 正規の利用者が持っている最新のトークンも失効し、再ログインが必要になる
        Assert.Null(await RotateAsync(rotated!.NewRefreshToken));
        var latest = (await TokensOfAsync(user.Id)).Last();
        Assert.Equal(RefreshTokenRevokedReason.ReuseDetected, latest.RevokedReason);
    }

    [Fact(DisplayName = "UT-905 有効期限切れのトークンでは再発行できない")]
    public async Task RotateAsync_ReturnsNull_WhenExpired()
    {
        var (_, token) = await IssueAsync();

        _time.Advance(Lifetime);

        Assert.Null(await RotateAsync(token));
    }

    [Fact(DisplayName = "UT-906 ログアウトしたトークンは猶予期間内でも再発行できない")]
    public async Task RotateAsync_ReturnsNull_AfterRevoke()
    {
        var (user, token) = await IssueAsync();
        await using (var context = _db.CreateContext())
        {
            await CreateService(context).RevokeAsync(token);
        }

        Assert.Null(await RotateAsync(token));
        var stored = Assert.Single(await TokensOfAsync(user.Id));
        Assert.Equal(RefreshTokenRevokedReason.Logout, stored.RevokedReason);
    }

    [Fact(DisplayName = "UT-907 存在しないトークンでは再発行できない")]
    public async Task RotateAsync_ReturnsNull_WhenTokenIsUnknown()
    {
        Assert.Null(await RotateAsync("unknown-refresh-token"));
    }
}
