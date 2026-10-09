using TaskManagementSystem.Api.Models;
using TaskManagementSystem.Api.Services;
using TaskManagementSystem.Api.Tests.Infrastructure;

namespace TaskManagementSystem.Api.Tests.Services;

public class MetricsServiceTests : IClassFixture<TestDatabaseFixture>
{
    private readonly TestDatabaseFixture _db;

    public MetricsServiceTests(TestDatabaseFixture db)
    {
        _db = db;
    }

    private static async Task<TaskItem> AddTaskAsync(
        TaskManagementSystem.Api.Data.AppDbContext ctx, long projectId, string status,
        long? assigneeId = null, int? points = null, DateOnly? due = null)
    {
        var task = new TaskItem
        {
            ProjectId = projectId,
            Title = TestData.Unique("task"),
            Status = status,
            AssigneeId = assigneeId,
            EstimatePoints = points,
            DueDate = due,
        };
        ctx.Tasks.Add(task);
        await ctx.SaveChangesAsync();
        return task;
    }

    [Fact(DisplayName = "M5 進捗・完了率・状態別件数を集計する")]
    public async Task Progress_And_StatusCounts()
    {
        await using var ctx = _db.CreateContext();
        var (project, owner) = await TestData.CreateProjectWithOwnerAsync(ctx);
        await AddTaskAsync(ctx, project.Id, "TODO");
        await AddTaskAsync(ctx, project.Id, "IN_PROGRESS");
        await AddTaskAsync(ctx, project.Id, "DONE");
        await AddTaskAsync(ctx, project.Id, "DONE");

        var m = await new MetricsService(ctx).GetProjectMetricsAsync(project.Id, owner.Id);

        Assert.NotNull(m);
        Assert.Equal(4, m!.Total);
        Assert.Equal(2, m.DoneCount);
        Assert.Equal(0.5, m.CompletionRate);
        // 既定ワークフロー TODO/IN_PROGRESS/DONE の順で件数が出る
        Assert.Equal(new[] { 1, 1, 2 }, m.StatusCounts.Select(s => s.Count).ToArray());
    }

    [Fact(DisplayName = "M5 期限超過は未完了かつ期日が過去のものだけ数える")]
    public async Task Overdue_OnlyOpenPastDue()
    {
        await using var ctx = _db.CreateContext();
        var (project, owner) = await TestData.CreateProjectWithOwnerAsync(ctx);
        var past = new DateOnly(2020, 1, 1);
        var future = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30));
        await AddTaskAsync(ctx, project.Id, "TODO", due: past);       // 期限超過
        await AddTaskAsync(ctx, project.Id, "DONE", due: past);       // 完了済みは対象外
        await AddTaskAsync(ctx, project.Id, "TODO", due: future);     // 未来は対象外
        await AddTaskAsync(ctx, project.Id, "TODO");                  // 期日なしは対象外

        var m = await new MetricsService(ctx).GetProjectMetricsAsync(project.Id, owner.Id);

        Assert.Equal(1, m!.OverdueCount);
    }

    [Fact(DisplayName = "M5 担当別の負荷は未完了のみ・未割り当ても集計する")]
    public async Task AssigneeLoad_OpenOnly()
    {
        await using var ctx = _db.CreateContext();
        var (project, owner) = await TestData.CreateProjectWithOwnerAsync(ctx);
        await AddTaskAsync(ctx, project.Id, "TODO", assigneeId: owner.Id, points: 3);
        await AddTaskAsync(ctx, project.Id, "IN_PROGRESS", assigneeId: owner.Id, points: 2);
        await AddTaskAsync(ctx, project.Id, "DONE", assigneeId: owner.Id, points: 5); // 完了は負荷から除外
        await AddTaskAsync(ctx, project.Id, "TODO", assigneeId: null, points: 1);     // 未割り当て

        var m = await new MetricsService(ctx).GetProjectMetricsAsync(project.Id, owner.Id);

        var ownerLoad = m!.AssigneeLoads.Single(a => a.AssigneeId == owner.Id);
        Assert.Equal(2, ownerLoad.OpenCount);
        Assert.Equal(5, ownerLoad.EstimatePoints);
        var unassigned = m.AssigneeLoads.Single(a => a.AssigneeId == null);
        Assert.Equal(1, unassigned.OpenCount);
        Assert.Equal("未割り当て", unassigned.Name);
    }

    [Fact(DisplayName = "M5 非所属ユーザーには指標を開示しない(null)")]
    public async Task NonMember_Null()
    {
        await using var ctx = _db.CreateContext();
        var (project, _) = await TestData.CreateProjectWithOwnerAsync(ctx);
        var outsider = await TestData.CreateUserAsync(ctx);

        Assert.Null(await new MetricsService(ctx).GetProjectMetricsAsync(project.Id, outsider.Id));
    }

    private static void AddHistory(
        TaskManagementSystem.Api.Data.AppDbContext ctx, long taskId, long changedBy, string toStatus, DateTime at)
    {
        ctx.TaskStatusHistories.Add(new TaskStatusHistory
        {
            TaskId = taskId,
            ChangedBy = changedBy,
            ToStatus = toStatus,
            CreatedAt = at, // Kind=Unspecified(timestamp without time zone)
        });
    }

    [Fact(DisplayName = "M5 開発指標: 履歴からサイクル/リードタイムとスループットを算出する")]
    public async Task DevMetrics_FromHistory()
    {
        await using var ctx = _db.CreateContext();
        var (project, owner) = await TestData.CreateProjectWithOwnerAsync(ctx);
        // 直近に完了したタスク。作成→+4h で着手→+12h で完了(サイクル8h・リード12h)
        var baseAt = DateTime.SpecifyKind(DateTime.UtcNow.AddHours(-20), DateTimeKind.Unspecified);
        var task = new TaskItem { ProjectId = project.Id, Title = TestData.Unique("task"), Status = "DONE", CreatedAt = baseAt };
        ctx.Tasks.Add(task);
        await ctx.SaveChangesAsync();
        AddHistory(ctx, task.Id, owner.Id, "IN_PROGRESS", baseAt.AddHours(4));
        AddHistory(ctx, task.Id, owner.Id, "DONE", baseAt.AddHours(12));
        await ctx.SaveChangesAsync();

        var m = await new MetricsService(ctx).GetProjectDevMetricsAsync(project.Id, owner.Id, 30);

        Assert.NotNull(m);
        Assert.Equal(30, m!.Days);
        Assert.Equal(8.0, m.AvgCycleTimeHours);
        Assert.Equal(12.0, m.AvgLeadTimeHours);
        Assert.Equal(1, m.CompletedInPeriod);
        Assert.Equal(30, m.Throughput.Count); // 期間の全日が 0 埋めで並ぶ
        Assert.Equal(1, m.Throughput.Sum(p => p.Count));
    }

    [Fact(DisplayName = "M5 開発指標: 履歴が無ければ平均は null・スループットは 0")]
    public async Task DevMetrics_NoHistory()
    {
        await using var ctx = _db.CreateContext();
        var (project, owner) = await TestData.CreateProjectWithOwnerAsync(ctx);
        await AddTaskAsync(ctx, project.Id, "TODO");

        var m = await new MetricsService(ctx).GetProjectDevMetricsAsync(project.Id, owner.Id, 14);

        Assert.Null(m!.AvgCycleTimeHours);
        Assert.Null(m.AvgLeadTimeHours);
        Assert.Equal(0, m.CompletedInPeriod);
        Assert.Equal(14, m.Throughput.Count);
    }

    [Fact(DisplayName = "M5 開発指標: 非所属ユーザーには開示しない(null)")]
    public async Task DevMetrics_NonMember_Null()
    {
        await using var ctx = _db.CreateContext();
        var (project, _) = await TestData.CreateProjectWithOwnerAsync(ctx);
        var outsider = await TestData.CreateUserAsync(ctx);

        Assert.Null(await new MetricsService(ctx).GetProjectDevMetricsAsync(project.Id, outsider.Id, 30));
    }
}
