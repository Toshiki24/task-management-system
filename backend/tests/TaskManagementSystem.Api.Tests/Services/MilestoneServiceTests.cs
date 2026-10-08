using Microsoft.EntityFrameworkCore;
using TaskManagementSystem.Api.Dtos.Milestones;
using TaskManagementSystem.Api.Models;
using TaskManagementSystem.Api.Services;
using TaskManagementSystem.Api.Tests.Infrastructure;

namespace TaskManagementSystem.Api.Tests.Services;

public class MilestoneServiceTests : IClassFixture<TestDatabaseFixture>
{
    private readonly TestDatabaseFixture _db;

    public MilestoneServiceTests(TestDatabaseFixture db)
    {
        _db = db;
    }

    private static MilestoneRequest Req(string name = "v1.0", string status = MilestoneStatus.Open) =>
        new(name, new DateOnly(2026, 12, 31), status);

    private static async Task<TaskItem> AddTaskAsync(
        TaskManagementSystem.Api.Data.AppDbContext ctx, long projectId, string status, int? points, long? milestoneId = null)
    {
        var task = new TaskItem
        {
            ProjectId = projectId,
            Title = TestData.Unique("task"),
            Status = status,
            EstimatePoints = points,
            MilestoneId = milestoneId,
        };
        ctx.Tasks.Add(task);
        await ctx.SaveChangesAsync();
        return task;
    }

    [Fact(DisplayName = "M3 OWNER/WS Admin はマイルストーンを作成でき、一般メンバーは 403")]
    public async Task Create_RequiresManager()
    {
        await using var ctx = _db.CreateContext();
        var (project, owner) = await TestData.CreateProjectWithOwnerAsync(ctx);
        var member = await TestData.CreateUserAsync(ctx);
        await TestData.AddMemberAsync(ctx, project.Id, member.Id, ProjectMemberRole.Member);
        var service = new MilestoneService(ctx);

        Assert.Equal(MilestoneResult.Forbidden, (await service.CreateAsync(project.Id, Req(), member.Id)).Result);
        var created = await service.CreateAsync(project.Id, Req(), owner.Id);
        Assert.Equal(MilestoneResult.Success, created.Result);
        Assert.Equal("v1.0", created.Data!.Name);
    }

    [Fact(DisplayName = "M3 一覧は進捗(完了/全数/見積合計)を返す")]
    public async Task List_WithProgress()
    {
        await using var ctx = _db.CreateContext();
        var (project, owner) = await TestData.CreateProjectWithOwnerAsync(ctx);
        var service = new MilestoneService(ctx);
        var milestone = (await service.CreateAsync(project.Id, Req(), owner.Id)).Data!;
        await AddTaskAsync(ctx, project.Id, "DONE", 3, milestone.Id);
        await AddTaskAsync(ctx, project.Id, "TODO", 5, milestone.Id);
        await AddTaskAsync(ctx, project.Id, "TODO", 2); // 未割り当て(集計対象外)

        var list = await service.GetByProjectAsync(project.Id, owner.Id);
        var dto = Assert.Single(list!);
        Assert.Equal(1, dto.Progress.Done);
        Assert.Equal(2, dto.Progress.Total);
        Assert.Equal(8, dto.Progress.Points);
    }

    [Fact(DisplayName = "M3 タスクをマイルストーンへ割り当て/未割り当てへ戻せる")]
    public async Task AssignAndUnassign()
    {
        await using var ctx = _db.CreateContext();
        var (project, owner) = await TestData.CreateProjectWithOwnerAsync(ctx);
        var service = new MilestoneService(ctx);
        var milestone = (await service.CreateAsync(project.Id, Req(), owner.Id)).Data!;
        var task = await AddTaskAsync(ctx, project.Id, "TODO", null);

        Assert.Equal(MilestoneResult.Success, await service.AssignTaskAsync(project.Id, task.Id, milestone.Id, owner.Id));
        ctx.ChangeTracker.Clear();
        Assert.Equal(milestone.Id, (await ctx.Tasks.FindAsync(task.Id))!.MilestoneId);

        Assert.Equal(MilestoneResult.Success, await service.AssignTaskAsync(project.Id, task.Id, null, owner.Id));
        ctx.ChangeTracker.Clear();
        Assert.Null((await ctx.Tasks.FindAsync(task.Id))!.MilestoneId);
    }

    [Fact(DisplayName = "M3 別プロジェクトのマイルストーンへの割り当ては InvalidMilestone")]
    public async Task Assign_ForeignMilestone_Rejected()
    {
        await using var ctx = _db.CreateContext();
        var (project, owner) = await TestData.CreateProjectWithOwnerAsync(ctx);
        var service = new MilestoneService(ctx);
        var task = await AddTaskAsync(ctx, project.Id, "TODO", null);
        var otherProject = await TestData.CreateProjectAsync(ctx);
        await TestData.AddMemberAsync(ctx, otherProject.Id, owner.Id, ProjectMemberRole.Owner);
        var foreign = (await service.CreateAsync(otherProject.Id, Req("別"), owner.Id)).Data!;

        Assert.Equal(MilestoneResult.InvalidMilestone,
            await service.AssignTaskAsync(project.Id, task.Id, foreign.Id, owner.Id));
    }

    [Fact(DisplayName = "M3 マイルストーン削除でタスクは未割り当てへ戻る(SET NULL)")]
    public async Task Delete_UnassignsTasks()
    {
        long projectId;
        long taskId;
        long milestoneId;
        await using (var ctx = _db.CreateContext())
        {
            var (project, owner) = await TestData.CreateProjectWithOwnerAsync(ctx);
            var service = new MilestoneService(ctx);
            var milestone = (await service.CreateAsync(project.Id, Req(), owner.Id)).Data!;
            var task = await AddTaskAsync(ctx, project.Id, "TODO", null, milestone.Id);
            Assert.Equal(MilestoneResult.Success, await service.DeleteAsync(milestone.Id, owner.Id));
            projectId = project.Id;
            taskId = task.Id;
            milestoneId = milestone.Id;
        }

        await using (var ctx = _db.CreateContext())
        {
            Assert.False(await ctx.Milestones.AnyAsync(m => m.Id == milestoneId));
            Assert.Null((await ctx.Tasks.FindAsync(taskId))!.MilestoneId);
            Assert.Equal(projectId, (await ctx.Tasks.FindAsync(taskId))!.ProjectId);
        }
    }

    [Fact(DisplayName = "M3 非所属ユーザーには一覧を開示しない(null)")]
    public async Task List_NonMember_Null()
    {
        await using var ctx = _db.CreateContext();
        var (project, _) = await TestData.CreateProjectWithOwnerAsync(ctx);
        var outsider = await TestData.CreateUserAsync(ctx);

        Assert.Null(await new MilestoneService(ctx).GetByProjectAsync(project.Id, outsider.Id));
    }
}
