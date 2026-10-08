using Microsoft.EntityFrameworkCore;
using TaskManagementSystem.Api.Dtos.Cycles;
using TaskManagementSystem.Api.Models;
using TaskManagementSystem.Api.Services;
using TaskManagementSystem.Api.Tests.Infrastructure;

namespace TaskManagementSystem.Api.Tests.Services;

public class CycleServiceTests : IClassFixture<TestDatabaseFixture>
{
    private readonly TestDatabaseFixture _db;

    public CycleServiceTests(TestDatabaseFixture db)
    {
        _db = db;
    }

    private static CycleRequest Req(string name = "Sprint 1", string status = CycleStatus.Planned) =>
        new(name, new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 14), status);

    private static async Task<TaskItem> AddTaskAsync(
        TaskManagementSystem.Api.Data.AppDbContext ctx, long projectId, string status, int? points, long? cycleId = null)
    {
        var task = new TaskItem
        {
            ProjectId = projectId,
            Title = TestData.Unique("task"),
            Status = status,
            EstimatePoints = points,
            CycleId = cycleId,
        };
        ctx.Tasks.Add(task);
        await ctx.SaveChangesAsync();
        return task;
    }

    [Fact(DisplayName = "M3 OWNER/WS Admin はサイクルを作成でき、一般メンバーは 403")]
    public async Task Create_RequiresManager()
    {
        await using var ctx = _db.CreateContext();
        var (project, owner) = await TestData.CreateProjectWithOwnerAsync(ctx);
        var member = await TestData.CreateUserAsync(ctx);
        await TestData.AddMemberAsync(ctx, project.Id, member.Id, ProjectMemberRole.Member);
        var service = new CycleService(ctx);

        Assert.Equal(CycleResult.Forbidden, (await service.CreateAsync(project.Id, Req(), member.Id)).Result);
        var created = await service.CreateAsync(project.Id, Req(), owner.Id);
        Assert.Equal(CycleResult.Success, created.Result);
        Assert.Equal("Sprint 1", created.Data!.Name);
    }

    [Fact(DisplayName = "M3 一覧は進捗(完了/全数/見積合計)を返す")]
    public async Task List_WithProgress()
    {
        await using var ctx = _db.CreateContext();
        var (project, owner) = await TestData.CreateProjectWithOwnerAsync(ctx);
        var service = new CycleService(ctx);
        var cycle = (await service.CreateAsync(project.Id, Req(), owner.Id)).Data!;
        await AddTaskAsync(ctx, project.Id, "DONE", 3, cycle.Id);
        await AddTaskAsync(ctx, project.Id, "TODO", 5, cycle.Id);
        await AddTaskAsync(ctx, project.Id, "TODO", 2); // バックログ(集計対象外)

        var list = await service.GetByProjectAsync(project.Id, owner.Id);
        var dto = Assert.Single(list!);
        Assert.Equal(1, dto.Progress.Done);
        Assert.Equal(2, dto.Progress.Total);
        Assert.Equal(8, dto.Progress.Points);
    }

    [Fact(DisplayName = "M3 タスクをサイクルへ割り当て/バックログへ戻せる")]
    public async Task AssignAndUnassign()
    {
        await using var ctx = _db.CreateContext();
        var (project, owner) = await TestData.CreateProjectWithOwnerAsync(ctx);
        var service = new CycleService(ctx);
        var cycle = (await service.CreateAsync(project.Id, Req(), owner.Id)).Data!;
        var task = await AddTaskAsync(ctx, project.Id, "TODO", null);

        Assert.Equal(CycleResult.Success, await service.AssignTaskAsync(project.Id, task.Id, cycle.Id, owner.Id));
        ctx.ChangeTracker.Clear();
        Assert.Equal(cycle.Id, (await ctx.Tasks.FindAsync(task.Id))!.CycleId);

        Assert.Equal(CycleResult.Success, await service.AssignTaskAsync(project.Id, task.Id, null, owner.Id));
        ctx.ChangeTracker.Clear();
        Assert.Null((await ctx.Tasks.FindAsync(task.Id))!.CycleId);
    }

    [Fact(DisplayName = "M3 別プロジェクトのサイクルへの割り当ては InvalidCycle")]
    public async Task Assign_ForeignCycle_Rejected()
    {
        await using var ctx = _db.CreateContext();
        var (project, owner) = await TestData.CreateProjectWithOwnerAsync(ctx);
        var service = new CycleService(ctx);
        var task = await AddTaskAsync(ctx, project.Id, "TODO", null);
        var otherProject = await TestData.CreateProjectAsync(ctx);
        // owner を other のメンバーにして other にサイクルを作る
        await TestData.AddMemberAsync(ctx, otherProject.Id, owner.Id, ProjectMemberRole.Owner);
        var foreignCycle = (await service.CreateAsync(otherProject.Id, Req("別"), owner.Id)).Data!;

        Assert.Equal(CycleResult.InvalidCycle,
            await service.AssignTaskAsync(project.Id, task.Id, foreignCycle.Id, owner.Id));
    }

    [Fact(DisplayName = "M3 サイクル削除でタスクはバックログへ戻る(SET NULL)")]
    public async Task Delete_MovesTasksToBacklog()
    {
        long projectId;
        long taskId;
        long cycleId;
        await using (var ctx = _db.CreateContext())
        {
            var (project, owner) = await TestData.CreateProjectWithOwnerAsync(ctx);
            var service = new CycleService(ctx);
            var cycle = (await service.CreateAsync(project.Id, Req(), owner.Id)).Data!;
            var task = await AddTaskAsync(ctx, project.Id, "TODO", null, cycle.Id);
            Assert.Equal(CycleResult.Success, await service.DeleteAsync(cycle.Id, owner.Id));
            projectId = project.Id;
            taskId = task.Id;
            cycleId = cycle.Id;
        }

        await using (var ctx = _db.CreateContext())
        {
            Assert.False(await ctx.Cycles.AnyAsync(c => c.Id == cycleId));
            Assert.Null((await ctx.Tasks.FindAsync(taskId))!.CycleId);
            Assert.Equal(projectId, (await ctx.Tasks.FindAsync(taskId))!.ProjectId);
        }
    }

    [Fact(DisplayName = "M3 非所属ユーザーには一覧を開示しない(null)")]
    public async Task List_NonMember_Null()
    {
        await using var ctx = _db.CreateContext();
        var (project, _) = await TestData.CreateProjectWithOwnerAsync(ctx);
        var outsider = await TestData.CreateUserAsync(ctx);

        Assert.Null(await new CycleService(ctx).GetByProjectAsync(project.Id, outsider.Id));
    }
}
