using Microsoft.EntityFrameworkCore;
using TaskManagementSystem.Api.Models;
using TaskManagementSystem.Api.Services;
using TaskManagementSystem.Api.Tests.Infrastructure;

namespace TaskManagementSystem.Api.Tests.Services;

public class DueNotificationServiceTests : IClassFixture<TestDatabaseFixture>
{
    private readonly TestDatabaseFixture _db;

    public DueNotificationServiceTests(TestDatabaseFixture db)
    {
        _db = db;
    }

    private static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.UtcNow);

    private static async Task<TaskItem> CreateDueTaskAsync(
        TaskManagementSystem.Api.Data.AppDbContext ctx, long projectId, long? assigneeId, DateOnly due, string status = "TODO")
    {
        var task = new TaskItem
        {
            ProjectId = projectId,
            AssigneeId = assigneeId,
            Title = TestData.Unique("期限タスク"),
            Status = status,
            DueDate = due,
        };
        ctx.Tasks.Add(task);
        await ctx.SaveChangesAsync();
        return task;
    }

    [Fact(DisplayName = "M3 期限超過は DUE_OVERDUE、担当とウォッチャーに届く")]
    public async Task Overdue_NotifiesAssigneeAndWatcher()
    {
        await using var ctx = _db.CreateContext();
        var (project, owner) = await TestData.CreateProjectWithOwnerAsync(ctx);
        var watcher = await TestData.CreateUserAsync(ctx);
        await TestData.AddMemberAsync(ctx, project.Id, watcher.Id, ProjectMemberRole.Member);
        var task = await CreateDueTaskAsync(ctx, project.Id, owner.Id, Today.AddDays(-1));
        ctx.TaskWatchers.Add(new TaskWatcher { TaskId = task.Id, UserId = watcher.Id });
        await ctx.SaveChangesAsync();

        var created = await new DueNotificationService(ctx).GenerateAsync(Today, 3);

        Assert.Equal(2, created);
        var types = await ctx.Notifications.Where(n => n.TaskId == task.Id).Select(n => n.Type).ToListAsync();
        Assert.All(types, t => Assert.Equal(NotificationType.DueOverdue, t));
    }

    [Fact(DisplayName = "M3 期限間近(within 日)は DUE_SOON、範囲外は生成しない")]
    public async Task Soon_WithinWindowOnly()
    {
        await using var ctx = _db.CreateContext();
        var (project, owner) = await TestData.CreateProjectWithOwnerAsync(ctx);
        var soon = await CreateDueTaskAsync(ctx, project.Id, owner.Id, Today.AddDays(2));
        var far = await CreateDueTaskAsync(ctx, project.Id, owner.Id, Today.AddDays(10));

        var created = await new DueNotificationService(ctx).GenerateAsync(Today, 3);

        Assert.Equal(1, created);
        Assert.Equal(NotificationType.DueSoon,
            (await ctx.Notifications.SingleAsync(n => n.TaskId == soon.Id)).Type);
        Assert.False(await ctx.Notifications.AnyAsync(n => n.TaskId == far.Id));
    }

    [Fact(DisplayName = "M3 完了(DONE)タスクは期限超過でも通知しない")]
    public async Task ClosedTask_NoNotification()
    {
        await using var ctx = _db.CreateContext();
        var (project, owner) = await TestData.CreateProjectWithOwnerAsync(ctx);
        var task = await CreateDueTaskAsync(ctx, project.Id, owner.Id, Today.AddDays(-5), status: "DONE");

        var created = await new DueNotificationService(ctx).GenerateAsync(Today, 3);

        Assert.Equal(0, created);
        Assert.False(await ctx.Notifications.AnyAsync(n => n.TaskId == task.Id));
    }

    [Fact(DisplayName = "M3 同一日の再実行は重複生成しない。翌日は再生成する")]
    public async Task Dedupe_SameDayThenNextDay()
    {
        await using var ctx = _db.CreateContext();
        var (project, owner) = await TestData.CreateProjectWithOwnerAsync(ctx);
        var task = await CreateDueTaskAsync(ctx, project.Id, owner.Id, Today.AddDays(-1));
        var service = new DueNotificationService(ctx);

        Assert.Equal(1, await service.GenerateAsync(Today, 3));
        // 同じ日の再実行は 0
        Assert.Equal(0, await service.GenerateAsync(Today, 3));
        // 翌日基準なら再生成する
        Assert.Equal(1, await service.GenerateAsync(Today.AddDays(1), 3));
    }
}
