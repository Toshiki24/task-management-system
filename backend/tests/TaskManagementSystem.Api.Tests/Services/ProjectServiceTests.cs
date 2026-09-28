using Microsoft.EntityFrameworkCore;
using TaskManagementSystem.Api.Dtos.Projects;
using TaskManagementSystem.Api.Models;
using TaskManagementSystem.Api.Services;
using TaskManagementSystem.Api.Tests.Infrastructure;

namespace TaskManagementSystem.Api.Tests.Services;

public class ProjectServiceTests : IClassFixture<TestDatabaseFixture>
{
    private readonly TestDatabaseFixture _db;

    public ProjectServiceTests(TestDatabaseFixture db)
    {
        _db = db;
    }

    private static ProjectRequest NewRequest(string? status = null) => new(
        TestData.Unique("ut-project"),
        "説明",
        status,
        new DateOnly(2026, 10, 1),
        new DateOnly(2026, 12, 31));

    [Fact(DisplayName = "UT-301 プロジェクト作成で作成者がOWNER登録される")]
    public async Task CreateAsync_RegistersCreatorAsOwner()
    {
        await using var arrange = _db.CreateContext();
        var creator = await TestData.CreateUserAsync(arrange);

        await using var context = _db.CreateContext();
        var created = await new ProjectService(context).CreateAsync(NewRequest(), creator.Id);

        await using var assert = _db.CreateContext();
        var members = await assert.ProjectMembers.Where(pm => pm.ProjectId == created.Id).ToListAsync();
        var member = Assert.Single(members);
        Assert.Equal(creator.Id, member.UserId);
        Assert.Equal(ProjectMemberRole.Owner, member.Role);
    }

    [Fact(DisplayName = "UT-302 プロジェクト作成とメンバー登録がトランザクションになっている")]
    public async Task CreateAsync_RollsBackProject_WhenMemberRegistrationFails()
    {
        // 存在しないユーザーを作成者として渡し、メンバー登録(2回目のSaveChanges)だけを外部キー違反で失敗させる
        var request = NewRequest();

        await using var context = _db.CreateContext();
        await Assert.ThrowsAsync<DbUpdateException>(
            () => new ProjectService(context).CreateAsync(request, TestData.NonExistentId));

        await using var assert = _db.CreateContext();
        Assert.False(await assert.Projects.AnyAsync(p => p.Name == request.Name));
    }

    [Fact(DisplayName = "UT-303 status省略時はACTIVEになる")]
    public async Task CreateAsync_DefaultsStatusToActive_WhenStatusIsNull()
    {
        await using var arrange = _db.CreateContext();
        var creator = await TestData.CreateUserAsync(arrange);

        await using var context = _db.CreateContext();
        var created = await new ProjectService(context).CreateAsync(NewRequest(status: null), creator.Id);

        Assert.Equal(ProjectStatus.Active, created.Status);
        await using var assert = _db.CreateContext();
        Assert.Equal(ProjectStatus.Active, (await assert.Projects.SingleAsync(p => p.Id == created.Id)).Status);
    }

    [Fact(DisplayName = "UT-304 更新時、name/description/startDate/endDateが上書きされる")]
    public async Task UpdateAsync_OverwritesAllFields()
    {
        await using var arrange = _db.CreateContext();
        var (project, owner) = await TestData.CreateProjectWithOwnerAsync(arrange);

        var request = new ProjectRequest(
            TestData.Unique("ut-updated"), "更新後の説明", ProjectStatus.Completed,
            new DateOnly(2027, 1, 1), new DateOnly(2027, 3, 31));

        await using var context = _db.CreateContext();
        var outcome = await new ProjectService(context).UpdateAsync(project.Id, request, owner.Id);

        Assert.Equal(UpdateProjectResult.Success, outcome.Result);
        await using var assert = _db.CreateContext();
        var saved = await assert.Projects.SingleAsync(p => p.Id == project.Id);
        Assert.Equal(request.Name, saved.Name);
        Assert.Equal(request.Description, saved.Description);
        Assert.Equal(request.Status, saved.Status);
        Assert.Equal(request.StartDate, saved.StartDate);
        Assert.Equal(request.EndDate, saved.EndDate);
    }

    [Fact(DisplayName = "UT-305 更新時、status省略で既存値が維持される")]
    public async Task UpdateAsync_KeepsStatus_WhenStatusIsNull()
    {
        await using var arrange = _db.CreateContext();
        var (project, owner) = await TestData.CreateProjectWithOwnerAsync(arrange, ProjectStatus.Archived);

        await using var context = _db.CreateContext();
        var outcome = await new ProjectService(context).UpdateAsync(project.Id, NewRequest(status: null), owner.Id);

        Assert.Equal(UpdateProjectResult.Success, outcome.Result);
        Assert.Equal(ProjectStatus.Archived, outcome.Data!.Status);
        await using var assert = _db.CreateContext();
        Assert.Equal(ProjectStatus.Archived, (await assert.Projects.SingleAsync(p => p.Id == project.Id)).Status);
    }

    [Fact(DisplayName = "UT-306 存在しないIDの更新")]
    public async Task UpdateAsync_ReturnsProjectNotFound_WhenNotExists()
    {
        await using var arrange = _db.CreateContext();
        var user = await TestData.CreateUserAsync(arrange);

        await using var context = _db.CreateContext();
        var outcome = await new ProjectService(context).UpdateAsync(TestData.NonExistentId, NewRequest(), user.Id);

        Assert.Equal(UpdateProjectResult.ProjectNotFound, outcome.Result);
    }

    [Fact(DisplayName = "UT-307 存在しないIDの削除")]
    public async Task DeleteAsync_ReturnsProjectNotFound_WhenNotExists()
    {
        await using var arrange = _db.CreateContext();
        var user = await TestData.CreateUserAsync(arrange);

        await using var context = _db.CreateContext();
        var result = await new ProjectService(context).DeleteAsync(TestData.NonExistentId, user.Id);

        Assert.Equal(DeleteProjectResult.ProjectNotFound, result);
    }

    [Fact(DisplayName = "UT-308 更新時にupdated_atが現在時刻に更新される")]
    public async Task UpdateAsync_SetsUpdatedAtToNow()
    {
        await using var arrange = _db.CreateContext();
        var (project, owner) = await TestData.CreateProjectWithOwnerAsync(arrange);
        await using (var before = _db.CreateContext())
        {
            project = await before.Projects.SingleAsync(p => p.Id == project.Id);
        }

        var executedAt = DateTime.UtcNow;
        await using var context = _db.CreateContext();
        await new ProjectService(context).UpdateAsync(project.Id, NewRequest(), owner.Id);

        await using var assert = _db.CreateContext();
        var saved = await assert.Projects.SingleAsync(p => p.Id == project.Id);
        Assert.True(saved.UpdatedAt > project.UpdatedAt, $"更新前 {project.UpdatedAt:O} / 更新後 {saved.UpdatedAt:O}");
        Assert.InRange(saved.UpdatedAt, executedAt.AddSeconds(-5), DateTime.UtcNow.AddSeconds(5));
    }

    [Fact(DisplayName = "UT-309 一覧は自分が所属しているプロジェクトのみ返す")]
    public async Task GetAllAsync_ReturnsOnlyProjectsTheUserBelongsTo()
    {
        await using var arrange = _db.CreateContext();
        var user = await TestData.CreateUserAsync(arrange);
        var owned = await TestData.CreateProjectAsync(arrange);
        await TestData.AddMemberAsync(arrange, owned.Id, user.Id, ProjectMemberRole.Owner);
        var joined = await TestData.CreateProjectAsync(arrange);
        await TestData.AddMemberAsync(arrange, joined.Id, user.Id, ProjectMemberRole.Member);
        var (others, _) = await TestData.CreateProjectWithOwnerAsync(arrange);

        await using var context = _db.CreateContext();
        var result = await new ProjectService(context).GetAllAsync(user.Id);

        Assert.Equal(new[] { owned.Id, joined.Id }, result.Select(p => p.Id).ToArray());
        Assert.DoesNotContain(result, p => p.Id == others.Id);
    }

    [Fact(DisplayName = "UT-310 所属していないプロジェクトの詳細取得")]
    public async Task GetByIdAsync_ReturnsNull_WhenUserIsNotMember()
    {
        await using var arrange = _db.CreateContext();
        var (project, _) = await TestData.CreateProjectWithOwnerAsync(arrange);
        var outsider = await TestData.CreateUserAsync(arrange);

        await using var context = _db.CreateContext();
        var result = await new ProjectService(context).GetByIdAsync(project.Id, outsider.Id);

        Assert.Null(result);
    }

    [Fact(DisplayName = "UT-311 所属していないプロジェクトの更新・削除")]
    public async Task UpdateAndDeleteAsync_ReturnProjectNotFound_WhenUserIsNotMember()
    {
        await using var arrange = _db.CreateContext();
        var (project, _) = await TestData.CreateProjectWithOwnerAsync(arrange);
        var outsider = await TestData.CreateUserAsync(arrange);

        await using var context = _db.CreateContext();
        var service = new ProjectService(context);
        var updateOutcome = await service.UpdateAsync(project.Id, NewRequest(), outsider.Id);
        var deleteResult = await service.DeleteAsync(project.Id, outsider.Id);

        Assert.Equal(UpdateProjectResult.ProjectNotFound, updateOutcome.Result);
        Assert.Equal(DeleteProjectResult.ProjectNotFound, deleteResult);
        await using var assert = _db.CreateContext();
        var saved = await assert.Projects.SingleAsync(p => p.Id == project.Id);
        Assert.Equal(project.Name, saved.Name);
    }

    [Fact(DisplayName = "UT-312 MEMBERによるプロジェクトの更新・削除")]
    public async Task UpdateAndDeleteAsync_ReturnForbidden_WhenUserIsMember()
    {
        await using var arrange = _db.CreateContext();
        var (project, _) = await TestData.CreateProjectWithOwnerAsync(arrange);
        var member = await TestData.CreateUserAsync(arrange);
        await TestData.AddMemberAsync(arrange, project.Id, member.Id, ProjectMemberRole.Member);

        await using var context = _db.CreateContext();
        var service = new ProjectService(context);
        var updateOutcome = await service.UpdateAsync(project.Id, NewRequest(), member.Id);
        var deleteResult = await service.DeleteAsync(project.Id, member.Id);

        Assert.Equal(UpdateProjectResult.Forbidden, updateOutcome.Result);
        Assert.Equal(DeleteProjectResult.Forbidden, deleteResult);
        await using var assert = _db.CreateContext();
        var saved = await assert.Projects.SingleAsync(p => p.Id == project.Id);
        Assert.Equal(project.Name, saved.Name);
    }
}
