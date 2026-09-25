using Microsoft.EntityFrameworkCore;
using TaskManagementSystem.Api.Dtos.ProjectMembers;
using TaskManagementSystem.Api.Models;
using TaskManagementSystem.Api.Services;
using TaskManagementSystem.Api.Tests.Infrastructure;

namespace TaskManagementSystem.Api.Tests.Services;

public class ProjectMemberServiceTests : IClassFixture<TestDatabaseFixture>
{
    private readonly TestDatabaseFixture _db;

    public ProjectMemberServiceTests(TestDatabaseFixture db)
    {
        _db = db;
    }

    [Fact(DisplayName = "UT-401 存在しないプロジェクトのメンバー一覧取得")]
    public async Task GetMembersAsync_ReturnsNull_WhenProjectNotExists()
    {
        await using var context = _db.CreateContext();
        var result = await new ProjectMemberService(context).GetMembersAsync(TestData.NonExistentId);

        Assert.Null(result);
    }

    [Fact(DisplayName = "UT-402 メンバー追加成功")]
    public async Task AddMemberAsync_ReturnsSuccess_WhenUserIsNotMember()
    {
        await using var arrange = _db.CreateContext();
        var project = await TestData.CreateProjectAsync(arrange);
        var user = await TestData.CreateUserAsync(arrange);

        await using var context = _db.CreateContext();
        var outcome = await new ProjectMemberService(context)
            .AddMemberAsync(project.Id, new AddMemberRequest(user.Id, ProjectMemberRole.Member));

        Assert.Equal(AddMemberResult.Success, outcome.Result);
        Assert.Equal(new MemberAddedDto(project.Id, user.Id, ProjectMemberRole.Member), outcome.Data);
        await using var assert = _db.CreateContext();
        Assert.True(await assert.ProjectMembers.AnyAsync(pm => pm.ProjectId == project.Id && pm.UserId == user.Id));
    }

    [Fact(DisplayName = "UT-403 存在しないプロジェクトへのメンバー追加")]
    public async Task AddMemberAsync_ReturnsProjectNotFound_WhenProjectNotExists()
    {
        await using var arrange = _db.CreateContext();
        var user = await TestData.CreateUserAsync(arrange);

        await using var context = _db.CreateContext();
        var outcome = await new ProjectMemberService(context)
            .AddMemberAsync(TestData.NonExistentId, new AddMemberRequest(user.Id, ProjectMemberRole.Member));

        Assert.Equal(AddMemberResult.ProjectNotFound, outcome.Result);
    }

    [Fact(DisplayName = "UT-404 存在しないユーザーの追加")]
    public async Task AddMemberAsync_ReturnsUserNotFound_WhenUserNotExists()
    {
        await using var arrange = _db.CreateContext();
        var project = await TestData.CreateProjectAsync(arrange);

        await using var context = _db.CreateContext();
        var outcome = await new ProjectMemberService(context)
            .AddMemberAsync(project.Id, new AddMemberRequest(TestData.NonExistentId, ProjectMemberRole.Member));

        Assert.Equal(AddMemberResult.UserNotFound, outcome.Result);
    }

    [Fact(DisplayName = "UT-405 重複メンバー追加")]
    public async Task AddMemberAsync_ReturnsAlreadyMember_WhenUserIsMember()
    {
        await using var arrange = _db.CreateContext();
        var project = await TestData.CreateProjectAsync(arrange);
        var user = await TestData.CreateUserAsync(arrange);
        await TestData.AddMemberAsync(arrange, project.Id, user.Id);

        await using var context = _db.CreateContext();
        var outcome = await new ProjectMemberService(context)
            .AddMemberAsync(project.Id, new AddMemberRequest(user.Id, ProjectMemberRole.Owner));

        Assert.Equal(AddMemberResult.AlreadyMember, outcome.Result);
    }

    [Fact(DisplayName = "UT-406 メンバー削除成功")]
    public async Task RemoveMemberAsync_ReturnsSuccess_AndDeletesRow()
    {
        await using var arrange = _db.CreateContext();
        var project = await TestData.CreateProjectAsync(arrange);
        var user = await TestData.CreateUserAsync(arrange);
        await TestData.AddMemberAsync(arrange, project.Id, user.Id);

        await using var context = _db.CreateContext();
        var result = await new ProjectMemberService(context).RemoveMemberAsync(project.Id, user.Id);

        Assert.Equal(RemoveMemberResult.Success, result);
        await using var assert = _db.CreateContext();
        Assert.False(await assert.ProjectMembers.AnyAsync(pm => pm.ProjectId == project.Id && pm.UserId == user.Id));
    }

    [Fact(DisplayName = "UT-407 存在しないメンバーの削除")]
    public async Task RemoveMemberAsync_ReturnsMemberNotFound_WhenUserIsNotMember()
    {
        await using var arrange = _db.CreateContext();
        var project = await TestData.CreateProjectAsync(arrange);
        var user = await TestData.CreateUserAsync(arrange);

        await using var context = _db.CreateContext();
        var result = await new ProjectMemberService(context).RemoveMemberAsync(project.Id, user.Id);

        Assert.Equal(RemoveMemberResult.MemberNotFound, result);
    }
}
