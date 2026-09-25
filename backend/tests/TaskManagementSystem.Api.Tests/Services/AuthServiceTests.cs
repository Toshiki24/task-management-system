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

    private AuthService CreateService(Api.Data.AppDbContext context) =>
        new(context, new JwtTokenService(JwtTestConfiguration.Create()));

    [Fact(DisplayName = "UT-101 正しいメール・パスワードでログイン成功")]
    public async Task LoginAsync_ReturnsTokenAndUser_WhenCredentialsAreValid()
    {
        await using var arrange = _db.CreateContext();
        var user = await TestData.CreateUserAsync(arrange);

        await using var context = _db.CreateContext();
        var result = await CreateService(context).LoginAsync(new LoginRequest(user.Email, TestData.DefaultPassword));

        Assert.NotNull(result);
        Assert.False(string.IsNullOrWhiteSpace(result.AccessToken));
        Assert.Equal(user.Id, result.User.Id);
        Assert.Equal(user.Name, result.User.Name);
        Assert.Equal(user.Email, result.User.Email);
    }

    [Fact(DisplayName = "UT-102 存在しないメールでログイン失敗")]
    public async Task LoginAsync_ReturnsNull_WhenEmailDoesNotExist()
    {
        await using var context = _db.CreateContext();
        var result = await CreateService(context).LoginAsync(
            new LoginRequest($"{TestData.Unique("missing")}@example.test", TestData.DefaultPassword));

        Assert.Null(result);
    }

    [Fact(DisplayName = "UT-103 パスワード不一致でログイン失敗")]
    public async Task LoginAsync_ReturnsNull_WhenPasswordIsWrong()
    {
        await using var arrange = _db.CreateContext();
        var user = await TestData.CreateUserAsync(arrange);

        await using var context = _db.CreateContext();
        var result = await CreateService(context).LoginAsync(new LoginRequest(user.Email, "WrongPassword!"));

        Assert.Null(result);
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
