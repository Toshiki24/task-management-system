using TaskManagementSystem.Api.Dtos.Comments;
using TaskManagementSystem.Api.Dtos.Tasks;
using TaskManagementSystem.Api.Models;
using TaskManagementSystem.Api.Services;
using TaskManagementSystem.Api.Tests.Infrastructure;

namespace TaskManagementSystem.Api.Tests.Services;

public class WatcherServiceTests : IClassFixture<TestDatabaseFixture>
{
    private readonly TestDatabaseFixture _db;

    public WatcherServiceTests(TestDatabaseFixture db)
    {
        _db = db;
    }

    private static TaskRequest Req(string title, long? assigneeId = null) =>
        new(assigneeId, title, null, null, null, null, null, null, null);

    [Fact(DisplayName = "M3 ウォッチ/解除ができ、Watching フラグに反映される")]
    public async Task WatchAndUnwatch_Toggles()
    {
        await using var ctx = _db.CreateContext();
        var (project, owner) = await TestData.CreateProjectWithOwnerAsync(ctx);
        var task = await TestData.CreateTaskAsync(ctx, project.Id);
        var service = new WatcherService(ctx);

        Assert.False((await service.GetWatchersAsync(task.Id, owner.Id))!.Watching);

        Assert.Equal(WatchResult.Success, await service.WatchAsync(task.Id, owner.Id));
        var afterWatch = await service.GetWatchersAsync(task.Id, owner.Id);
        Assert.True(afterWatch!.Watching);
        Assert.Contains(afterWatch.Watchers, w => w.UserId == owner.Id);

        // 冪等(二重ウォッチしても増えない)
        Assert.Equal(WatchResult.Success, await service.WatchAsync(task.Id, owner.Id));
        Assert.Single((await service.GetWatchersAsync(task.Id, owner.Id))!.Watchers);

        Assert.Equal(WatchResult.Success, await service.UnwatchAsync(task.Id, owner.Id));
        Assert.False((await service.GetWatchersAsync(task.Id, owner.Id))!.Watching);
    }

    [Fact(DisplayName = "M3 担当者はタスク作成・更新で自動ウォッチされる")]
    public async Task Assignee_AutoWatched()
    {
        await using var ctx = _db.CreateContext();
        var (project, owner) = await TestData.CreateProjectWithOwnerAsync(ctx);
        var member = await TestData.CreateUserAsync(ctx);
        await TestData.AddMemberAsync(ctx, project.Id, member.Id, ProjectMemberRole.Member);
        var taskService = new TaskService(ctx);
        var watcherService = new WatcherService(ctx);

        var created = (await taskService.CreateAsync(project.Id, Req("担当付き", member.Id), owner.Id)).Data!;

        var watchers = await watcherService.GetWatchersAsync(created.Id, owner.Id);
        Assert.Contains(watchers!.Watchers, w => w.UserId == member.Id);
    }

    [Fact(DisplayName = "M3 コメント投稿者と被メンション者が自動ウォッチされる")]
    public async Task CommenterAndMentioned_AutoWatched()
    {
        await using var ctx = _db.CreateContext();
        var (project, owner) = await TestData.CreateProjectWithOwnerAsync(ctx);
        var member = await TestData.CreateUserAsync(ctx);
        await TestData.AddMemberAsync(ctx, project.Id, member.Id, ProjectMemberRole.Member);
        var task = await TestData.CreateTaskAsync(ctx, project.Id);
        var commentService = new CommentService(ctx);
        var watcherService = new WatcherService(ctx);

        await commentService.CreateAsync(task.Id, owner.Id, new CommentRequest($"@{member.Name} 確認を"));

        var watchers = (await watcherService.GetWatchersAsync(task.Id, owner.Id))!.Watchers;
        Assert.Contains(watchers, w => w.UserId == owner.Id); // 投稿者
        Assert.Contains(watchers, w => w.UserId == member.Id); // 被メンション
    }

    [Fact(DisplayName = "M3 非所属ユーザーには 404 相当(null / TaskNotFound)")]
    public async Task NonMember_Denied()
    {
        await using var ctx = _db.CreateContext();
        var (project, _) = await TestData.CreateProjectWithOwnerAsync(ctx);
        var outsider = await TestData.CreateUserAsync(ctx);
        var task = await TestData.CreateTaskAsync(ctx, project.Id);
        var service = new WatcherService(ctx);

        Assert.Null(await service.GetWatchersAsync(task.Id, outsider.Id));
        Assert.Equal(WatchResult.TaskNotFound, await service.WatchAsync(task.Id, outsider.Id));
    }
}
