using Microsoft.Extensions.Configuration;
using TaskManagementSystem.Api.Dtos.Auth;
using TaskManagementSystem.Api.Services;
using TaskManagementSystem.Api.Tests.Infrastructure;

namespace TaskManagementSystem.Api.Tests.Services;

public class AuthServiceTests : IClassFixture<TestDatabaseFixture>
{
    private readonly TestDatabaseFixture _db;

    public AuthServiceTests(TestDatabaseFixture db)
    {
        _db = db;
    }

    private readonly ManualTimeProvider _time = new();

    private AuthService CreateService(Api.Data.AppDbContext context, ILoginAttemptLimiter? limiter = null) =>
        new(
            context,
            new JwtTokenService(JwtTestConfiguration.Create()),
            limiter ?? new LoginAttemptLimiter(LoginAttemptLimiterTests.CreateConfiguration(), _time),
            new RefreshTokenService(context, _time, RefreshTokenServiceTests.CreateConfiguration()));

    [Fact(DisplayName = "UT-101 正しいメール・パスワードでログイン成功")]
    public async Task LoginAsync_ReturnsTokenAndUser_WhenCredentialsAreValid()
    {
        await using var arrange = _db.CreateContext();
        var user = await TestData.CreateUserAsync(arrange);

        await using var context = _db.CreateContext();
        var outcome = await CreateService(context).LoginAsync(new LoginRequest(user.Email, TestData.DefaultPassword));

        Assert.Equal(LoginResult.Success, outcome.Result);
        var result = outcome.Data;
        Assert.NotNull(result);
        Assert.False(string.IsNullOrWhiteSpace(result.AccessToken));
        Assert.False(string.IsNullOrWhiteSpace(result.RefreshToken));
        Assert.True(result.AccessTokenExpiresAt > DateTime.UtcNow);
        Assert.Equal(user.Id, result.User.Id);
        Assert.Equal(user.Name, result.User.Name);
        Assert.Equal(user.Email, result.User.Email);
    }

    [Fact(DisplayName = "UT-102 存在しないメールでログイン失敗")]
    public async Task LoginAsync_ReturnsInvalidCredentials_WhenEmailDoesNotExist()
    {
        await using var context = _db.CreateContext();
        var outcome = await CreateService(context).LoginAsync(
            new LoginRequest($"{TestData.Unique("missing")}@example.test", TestData.DefaultPassword));

        Assert.Equal(LoginResult.InvalidCredentials, outcome.Result);
        Assert.Null(outcome.Data);
    }

    [Fact(DisplayName = "UT-103 パスワード不一致でログイン失敗")]
    public async Task LoginAsync_ReturnsInvalidCredentials_WhenPasswordIsWrong()
    {
        await using var arrange = _db.CreateContext();
        var user = await TestData.CreateUserAsync(arrange);

        await using var context = _db.CreateContext();
        var outcome = await CreateService(context).LoginAsync(new LoginRequest(user.Email, "WrongPassword!"));

        Assert.Equal(LoginResult.InvalidCredentials, outcome.Result);
        Assert.Null(outcome.Data);
    }

    [Fact(DisplayName = "UT-104 パスワードがBCryptで検証される")]
    public async Task Password_IsStoredAsBCryptHash_AndVerifiedByBCrypt()
    {
        await using var arrange = _db.CreateContext();
        var user = await TestData.CreateUserAsync(arrange);

        Assert.NotEqual(TestData.DefaultPassword, user.PasswordHash);
        Assert.StartsWith("$2", user.PasswordHash);
        Assert.True(BCrypt.Net.BCrypt.Verify(TestData.DefaultPassword, user.PasswordHash));
        Assert.False(BCrypt.Net.BCrypt.Verify("WrongPassword!", user.PasswordHash));
    }

    [Fact(DisplayName = "UT-105 失敗回数が上限に達すると、正しいパスワードでもログインできない")]
    public async Task LoginAsync_ReturnsTooManyAttempts_WhenFailuresReachLimit()
    {
        await using var arrange = _db.CreateContext();
        var user = await TestData.CreateUserAsync(arrange);
        using var limiter = new LoginAttemptLimiter(LoginAttemptLimiterTests.CreateConfiguration(), _time);

        await using var context = _db.CreateContext();
        var service = CreateService(context, limiter);
        for (var i = 0; i < 5; i++)
        {
            Assert.Equal(
                LoginResult.InvalidCredentials,
                (await service.LoginAsync(new LoginRequest(user.Email, "WrongPassword!"))).Result);
        }

        var outcome = await service.LoginAsync(new LoginRequest(user.Email, TestData.DefaultPassword));

        Assert.Equal(LoginResult.TooManyAttempts, outcome.Result);
        Assert.Null(outcome.Data);
        Assert.Equal(TimeSpan.FromSeconds(60), outcome.RetryAfter);
    }

    [Fact(DisplayName = "UT-106 存在しないメールアドレスの失敗も数えられ、ログイン成功で失敗回数がリセットされる")]
    public async Task LoginAsync_RecordsFailuresForUnknownEmail_AndResetsOnSuccess()
    {
        await using var arrange = _db.CreateContext();
        var user = await TestData.CreateUserAsync(arrange);
        var unknownEmail = $"{TestData.Unique("missing")}@example.test";
        using var limiter = new LoginAttemptLimiter(LoginAttemptLimiterTests.CreateConfiguration(), _time);

        await using var context = _db.CreateContext();
        var service = CreateService(context, limiter);
        for (var i = 0; i < 5; i++)
        {
            await service.LoginAsync(new LoginRequest(unknownEmail, "WrongPassword!"));
            await service.LoginAsync(new LoginRequest(user.Email, "WrongPassword!"));
            if (i == 3)
            {
                // 4回失敗した時点でログインに成功させ、失敗回数をリセットする
                Assert.Equal(
                    LoginResult.Success,
                    (await service.LoginAsync(new LoginRequest(user.Email, TestData.DefaultPassword))).Result);
            }
        }

        Assert.NotNull(limiter.GetRetryAfter(unknownEmail));
        Assert.Null(limiter.GetRetryAfter(user.Email));
    }

    [Fact(DisplayName = "UT-107 リフレッシュトークンでアクセストークンとリフレッシュトークンが再発行される")]
    public async Task RefreshAsync_ReturnsNewTokens()
    {
        await using var arrange = _db.CreateContext();
        var user = await TestData.CreateUserAsync(arrange);
        await using var loginContext = _db.CreateContext();
        var login = (await CreateService(loginContext).LoginAsync(new LoginRequest(user.Email, TestData.DefaultPassword))).Data!;

        await using var context = _db.CreateContext();
        var refreshed = await CreateService(context).RefreshAsync(new RefreshTokenRequest(login.RefreshToken));

        Assert.NotNull(refreshed);
        Assert.False(string.IsNullOrWhiteSpace(refreshed.AccessToken));
        Assert.NotEqual(login.RefreshToken, refreshed.RefreshToken);
        Assert.Equal(user.Id, refreshed.User.Id);
    }

    [Fact(DisplayName = "UT-108 ログアウトしたリフレッシュトークンでは再発行できない")]
    public async Task RefreshAsync_ReturnsNull_AfterLogout()
    {
        await using var arrange = _db.CreateContext();
        var user = await TestData.CreateUserAsync(arrange);
        await using var loginContext = _db.CreateContext();
        var login = (await CreateService(loginContext).LoginAsync(new LoginRequest(user.Email, TestData.DefaultPassword))).Data!;
        await using var logoutContext = _db.CreateContext();
        await CreateService(logoutContext).LogoutAsync(new RefreshTokenRequest(login.RefreshToken));

        await using var context = _db.CreateContext();
        var refreshed = await CreateService(context).RefreshAsync(new RefreshTokenRequest(login.RefreshToken));

        Assert.Null(refreshed);
    }
}

internal static class JwtTestConfiguration
{
    public const string Issuer = "TaskManagementSystem";
    public const string Audience = "TaskManagementSystem";
    public const string Key = "unit-test-only-signing-key-0123456789-0123456789-0123456789";

    public static IConfiguration Create(int expiresMinutes = 60) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Key"] = Key,
                ["Jwt:Issuer"] = Issuer,
                ["Jwt:Audience"] = Audience,
                ["Jwt:ExpiresMinutes"] = expiresMinutes.ToString(),
            })
            .Build();
}
