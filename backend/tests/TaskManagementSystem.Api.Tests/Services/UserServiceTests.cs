using TaskManagementSystem.Api.Dtos.Common;
using TaskManagementSystem.Api.Services;
using TaskManagementSystem.Api.Tests.Infrastructure;

namespace TaskManagementSystem.Api.Tests.Services;

public class UserServiceTests : IClassFixture<TestDatabaseFixture>
{
    private readonly TestDatabaseFixture _db;

    public UserServiceTests(TestDatabaseFixture db)
    {
        _db = db;
    }

    [Fact(DisplayName = "UT-201 ユーザー一覧取得")]
    public async Task GetAllAsync_ReturnsAllUsersOrderedById_WithoutPasswordHash()
    {
        await using var arrange = _db.CreateContext();
        var user1 = await TestData.CreateUserAsync(arrange);
        var user2 = await TestData.CreateUserAsync(arrange);

        await using var context = _db.CreateContext();
        var users = await new UserService(context).GetAllAsync();

        // 他のテストが作成したユーザーも含まれ得るため、件数ではなく包含と並び順で検証する
        Assert.Contains(users, u => u.Id == user1.Id && u.Email == user1.Email);
        Assert.Contains(users, u => u.Id == user2.Id && u.Email == user2.Email);
        Assert.Equal(users.Select(u => u.Id).Order(), users.Select(u => u.Id));
        Assert.DoesNotContain(
            typeof(UserDto).GetProperties(),
            p => p.Name.Contains("Password", StringComparison.OrdinalIgnoreCase));
    }

    [Fact(DisplayName = "UT-202 ユーザー詳細取得（存在する）")]
    public async Task GetByIdAsync_ReturnsUser_WhenExists()
    {
        await using var arrange = _db.CreateContext();
        var user = await TestData.CreateUserAsync(arrange);

        await using var context = _db.CreateContext();
        var result = await new UserService(context).GetByIdAsync(user.Id);

        Assert.NotNull(result);
        Assert.Equal(new UserDto(user.Id, user.Name, user.Email), result);
    }

    [Fact(DisplayName = "UT-203 ユーザー詳細取得（存在しない）")]
    public async Task GetByIdAsync_ReturnsNull_WhenNotExists()
    {
        await using var context = _db.CreateContext();
        var result = await new UserService(context).GetByIdAsync(TestData.NonExistentId);

        Assert.Null(result);
    }
}
