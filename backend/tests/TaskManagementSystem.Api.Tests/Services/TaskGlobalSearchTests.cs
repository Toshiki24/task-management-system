using TaskManagementSystem.Api.Models;
using TaskManagementSystem.Api.Services;
using TaskManagementSystem.Api.Tests.Infrastructure;

namespace TaskManagementSystem.Api.Tests.Services;

public class TaskGlobalSearchTests : IClassFixture<TestDatabaseFixture>
{
    private readonly TestDatabaseFixture _db;

    public TaskGlobalSearchTests(TestDatabaseFixture db)
    {
        _db = db;
    }

    private static async Task<TaskItem> CreateTitledTaskAsync(
        TaskManagementSystem.Api.Data.AppDbContext ctx, long projectId, string title)
    {
        var task = new TaskItem { ProjectId = projectId, Title = title };
        ctx.Tasks.Add(task);
        await ctx.SaveChangesAsync();
        return task;
    }

    [Fact(DisplayName = "M2 キーワードで閲覧可能なタスクを横断検索できる(タイトル一致)")]
    public async Task Search_MatchesByKeyword()
    {
        await using var ctx = _db.CreateContext();
        var (project, owner) = await TestData.CreateProjectWithOwnerAsync(ctx);
        var service = new TaskService(ctx);
        var hit = await CreateTitledTaskAsync(ctx, project.Id, TestData.Unique("リリース準備"));
        await CreateTitledTaskAsync(ctx, project.Id, TestData.Unique("無関係タスク"));

        var results = await service.SearchVisibleTasksAsync(owner.Id, "リリース", 20);

        Assert.Contains(results, r => r.Id == hit.Id);
        Assert.All(results, r => Assert.Contains("リリース", r.Title));
        Assert.Equal(project.Id, results.First(r => r.Id == hit.Id).ProjectId);
    }

    [Fact(DisplayName = "M2 所属しないワークスペースのタスクは検索結果に出ない")]
    public async Task Search_ExcludesInvisibleTasks()
    {
        await using var ctx = _db.CreateContext();
        var (myProject, me) = await TestData.CreateProjectWithOwnerAsync(ctx);
        var service = new TaskService(ctx);
        await CreateTitledTaskAsync(ctx, myProject.Id, "共通キーワードA");

        // 自分が所属しない別ワークスペースのプロジェクト
        var otherProject = await TestData.CreateProjectAsync(ctx);
        var foreign = await CreateTitledTaskAsync(ctx, otherProject.Id, "共通キーワードA");

        var results = await service.SearchVisibleTasksAsync(me.Id, "共通キーワードA", 20);

        Assert.DoesNotContain(results, r => r.Id == foreign.Id);
        Assert.All(results, r => Assert.Equal(myProject.Id, r.ProjectId));
    }

    [Fact(DisplayName = "M2 キーワード未指定でも閲覧可能なタスクを返し、limit で件数を絞る")]
    public async Task Search_NoKeyword_RespectsLimit()
    {
        await using var ctx = _db.CreateContext();
        var (project, owner) = await TestData.CreateProjectWithOwnerAsync(ctx);
        var service = new TaskService(ctx);
        for (var i = 0; i < 5; i++)
        {
            await CreateTitledTaskAsync(ctx, project.Id, TestData.Unique("タスク"));
        }

        var results = await service.SearchVisibleTasksAsync(owner.Id, null, 3);

        Assert.Equal(3, results.Count);
    }
}
