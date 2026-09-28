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
        await using var arrange = _db.CreateContext();
        var user = await TestData.CreateUserAsync(arrange);

        await using var context = _db.CreateContext();
        var result = await new ProjectMemberService(context).GetMembersAsync(TestData.NonExistentId, user.Id);

        Assert.Null(result);
    }

    [Fact(DisplayName = "UT-402 メンバー追加成功")]
    public async Task AddMemberAsync_ReturnsSuccess_WhenUserIsNotMember()
    {
        await using var arrange = _db.CreateContext();
        var (project, owner) = await TestData.CreateProjectWithOwnerAsync(arrange);
        var user = await TestData.CreateUserAsync(arrange);

        await using var context = _db.CreateContext();
        var outcome = await new ProjectMemberService(context)
            .AddMemberAsync(project.Id, new AddMemberRequest(user.Id, ProjectMemberRole.Member), owner.Id);

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
            .AddMemberAsync(TestData.NonExistentId, new AddMemberRequest(user.Id, ProjectMemberRole.Member), user.Id);

        Assert.Equal(AddMemberResult.ProjectNotFound, outcome.Result);
    }

    [Fact(DisplayName = "UT-404 存在しないユーザーの追加")]
    public async Task AddMemberAsync_ReturnsUserNotFound_WhenUserNotExists()
    {
        await using var arrange = _db.CreateContext();
        var (project, owner) = await TestData.CreateProjectWithOwnerAsync(arrange);

        await using var context = _db.CreateContext();
        var outcome = await new ProjectMemberService(context)
            .AddMemberAsync(project.Id, new AddMemberRequest(TestData.NonExistentId, ProjectMemberRole.Member), owner.Id);

        Assert.Equal(AddMemberResult.UserNotFound, outcome.Result);
    }

    [Fact(DisplayName = "UT-405 重複メンバー追加")]
    public async Task AddMemberAsync_ReturnsAlreadyMember_WhenUserIsMember()
    {
        await using var arrange = _db.CreateContext();
        var (project, owner) = await TestData.CreateProjectWithOwnerAsync(arrange);
        var user = await TestData.CreateUserAsync(arrange);
        await TestData.AddMemberAsync(arrange, project.Id, user.Id);

        await using var context = _db.CreateContext();
        var outcome = await new ProjectMemberService(context)
            .AddMemberAsync(project.Id, new AddMemberRequest(user.Id, ProjectMemberRole.Owner), owner.Id);

        Assert.Equal(AddMemberResult.AlreadyMember, outcome.Result);
    }

    [Fact(DisplayName = "UT-406 メンバー削除成功")]
    public async Task RemoveMemberAsync_ReturnsSuccess_AndDeletesRow()
    {
        await using var arrange = _db.CreateContext();
        var (project, owner) = await TestData.CreateProjectWithOwnerAsync(arrange);
        var user = await TestData.CreateUserAsync(arrange);
        await TestData.AddMemberAsync(arrange, project.Id, user.Id);

        await using var context = _db.CreateContext();
        var result = await new ProjectMemberService(context).RemoveMemberAsync(project.Id, user.Id, owner.Id);

        Assert.Equal(RemoveMemberResult.Success, result);
        await using var assert = _db.CreateContext();
        Assert.False(await assert.ProjectMembers.AnyAsync(pm => pm.ProjectId == project.Id && pm.UserId == user.Id));
    }

    [Fact(DisplayName = "UT-407 存在しないメンバーの削除")]
    public async Task RemoveMemberAsync_ReturnsMemberNotFound_WhenUserIsNotMember()
    {
        await using var arrange = _db.CreateContext();
        var (project, owner) = await TestData.CreateProjectWithOwnerAsync(arrange);
        var user = await TestData.CreateUserAsync(arrange);

        await using var context = _db.CreateContext();
        var result = await new ProjectMemberService(context).RemoveMemberAsync(project.Id, user.Id, owner.Id);

        Assert.Equal(RemoveMemberResult.MemberNotFound, result);
    }

    [Fact(DisplayName = "UT-408 所属していないプロジェクトのメンバー操作")]
    public async Task MemberOperations_ReturnProjectNotFound_WhenUserIsNotMember()
    {
        await using var arrange = _db.CreateContext();
        var (project, owner) = await TestData.CreateProjectWithOwnerAsync(arrange);
        var outsider = await TestData.CreateUserAsync(arrange);

        await using var context = _db.CreateContext();
        var service = new ProjectMemberService(context);
        var members = await service.GetMembersAsync(project.Id, outsider.Id);
        // 非メンバーが自分自身をOWNERとしてプロジェクトに追加しようとするケース
        var addOutcome = await service.AddMemberAsync(
            project.Id, new AddMemberRequest(outsider.Id, ProjectMemberRole.Owner), outsider.Id);
        var removeResult = await service.RemoveMemberAsync(project.Id, owner.Id, outsider.Id);

        Assert.Null(members);
        Assert.Equal(AddMemberResult.ProjectNotFound, addOutcome.Result);
        Assert.Equal(RemoveMemberResult.ProjectNotFound, removeResult);
        await using var assert = _db.CreateContext();
        Assert.False(await assert.ProjectMembers.AnyAsync(pm => pm.ProjectId == project.Id && pm.UserId == outsider.Id));
        Assert.True(await assert.ProjectMembers.AnyAsync(pm => pm.ProjectId == project.Id && pm.UserId == owner.Id));
    }

    [Fact(DisplayName = "UT-409 MEMBERによるメンバーの追加・削除")]
    public async Task AddAndRemoveMemberAsync_ReturnForbidden_WhenUserIsMember()
    {
        await using var arrange = _db.CreateContext();
        var (project, owner) = await TestData.CreateProjectWithOwnerAsync(arrange);
        var member = await TestData.CreateUserAsync(arrange);
        await TestData.AddMemberAsync(arrange, project.Id, member.Id, ProjectMemberRole.Member);
        var user = await TestData.CreateUserAsync(arrange);

        await using var context = _db.CreateContext();
        var service = new ProjectMemberService(context);
        var addOutcome = await service.AddMemberAsync(
            project.Id, new AddMemberRequest(user.Id, ProjectMemberRole.Member), member.Id);
        var removeResult = await service.RemoveMemberAsync(project.Id, owner.Id, member.Id);

        Assert.Equal(AddMemberResult.Forbidden, addOutcome.Result);
        Assert.Equal(RemoveMemberResult.Forbidden, removeResult);
        await using var assert = _db.CreateContext();
        Assert.False(await assert.ProjectMembers.AnyAsync(pm => pm.ProjectId == project.Id && pm.UserId == user.Id));
        Assert.True(await assert.ProjectMembers.AnyAsync(pm => pm.ProjectId == project.Id && pm.UserId == owner.Id));
    }

    [Fact(DisplayName = "UT-410 MEMBERはメンバー一覧を取得できる")]
    public async Task GetMembersAsync_ReturnsMembers_WhenUserIsMember()
    {
        await using var arrange = _db.CreateContext();
        var (project, owner) = await TestData.CreateProjectWithOwnerAsync(arrange);
        var member = await TestData.CreateUserAsync(arrange);
        await TestData.AddMemberAsync(arrange, project.Id, member.Id, ProjectMemberRole.Member);

        await using var context = _db.CreateContext();
        var result = await new ProjectMemberService(context).GetMembersAsync(project.Id, member.Id);

        Assert.NotNull(result);
        Assert.Equal(new[] { owner.Id, member.Id }, result.Select(m => m.UserId).ToArray());
    }

    [Fact(DisplayName = "UT-411 プロジェクトの最後のOWNERは削除できない")]
    public async Task RemoveMemberAsync_ReturnsLastOwner_WhenRemovingOnlyOwner()
    {
        await using var arrange = _db.CreateContext();
        var (project, owner) = await TestData.CreateProjectWithOwnerAsync(arrange);

        await using var context = _db.CreateContext();
        var result = await new ProjectMemberService(context).RemoveMemberAsync(project.Id, owner.Id, owner.Id);

        Assert.Equal(RemoveMemberResult.LastOwner, result);
        await using var assert = _db.CreateContext();
        Assert.True(await assert.ProjectMembers.AnyAsync(pm => pm.ProjectId == project.Id && pm.UserId == owner.Id));
    }

    [Fact(DisplayName = "UT-412 OWNERが複数いれば、OWNERを削除できる")]
    public async Task RemoveMemberAsync_RemovesOwner_WhenAnotherOwnerExists()
    {
        await using var arrange = _db.CreateContext();
        var (project, owner) = await TestData.CreateProjectWithOwnerAsync(arrange);
        var secondOwner = await TestData.CreateUserAsync(arrange);
        await TestData.AddMemberAsync(arrange, project.Id, secondOwner.Id, ProjectMemberRole.Owner);

        await using var context = _db.CreateContext();
        var result = await new ProjectMemberService(context).RemoveMemberAsync(project.Id, owner.Id, secondOwner.Id);

        Assert.Equal(RemoveMemberResult.Success, result);
    }

    [Fact(DisplayName = "UT-413 メンバーを削除すると、そのプロジェクトで担当していたタスクは未割り当てになる")]
    public async Task RemoveMemberAsync_UnassignsTasksInTheProject()
    {
        await using var arrange = _db.CreateContext();
        var (project, owner) = await TestData.CreateProjectWithOwnerAsync(arrange);
        var member = await TestData.CreateUserAsync(arrange);
        await TestData.AddMemberAsync(arrange, project.Id, member.Id);
        var task = await TestData.CreateTaskAsync(arrange, project.Id, member.Id);
        var (otherProject, _) = await TestData.CreateProjectWithOwnerAsync(arrange);
        await TestData.AddMemberAsync(arrange, otherProject.Id, member.Id);
        var otherTask = await TestData.CreateTaskAsync(arrange, otherProject.Id, member.Id);

        await using var context = _db.CreateContext();
        var result = await new ProjectMemberService(context).RemoveMemberAsync(project.Id, member.Id, owner.Id);

        Assert.Equal(RemoveMemberResult.Success, result);
        await using var assert = _db.CreateContext();
        Assert.Null((await assert.Tasks.SingleAsync(t => t.Id == task.Id)).AssigneeId);
        // 別のプロジェクトのタスクは影響を受けない
        Assert.Equal(member.Id, (await assert.Tasks.SingleAsync(t => t.Id == otherTask.Id)).AssigneeId);
    }
}
