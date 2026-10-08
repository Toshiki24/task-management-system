using TaskManagementSystem.Api.Dtos.Comments;
using TaskManagementSystem.Api.Dtos.Tasks;
using TaskManagementSystem.Api.Models;
using TaskManagementSystem.Api.Services;
using TaskManagementSystem.Api.Tests.Infrastructure;

namespace TaskManagementSystem.Api.Tests.Services;

public class ActivityServiceTests : IClassFixture<TestDatabaseFixture>
{
    private readonly TestDatabaseFixture _db;

    public ActivityServiceTests(TestDatabaseFixture db)
    {
        _db = db;
    }

    private static TaskRequest Req(
        string title, string? status = null, long? assigneeId = null) =>
        new(assigneeId, title, null, status, null, null, null, null, null);

    [Fact(DisplayName = "M3 タスク作成で CREATED アクティビティが記録される")]
    public async Task Create_RecordsCreated()
    {
        await using var ctx = _db.CreateContext();
        var (project, owner) = await TestData.CreateProjectWithOwnerAsync(ctx);
        var taskService = new TaskService(ctx);
        var activityService = new ActivityService(ctx);

        var created = (await taskService.CreateAsync(project.Id, Req("新タスク"), owner.Id)).Data!;

        var activities = await activityService.GetByTaskAsync(created.Id, owner.Id);
        var act = Assert.Single(activities!);
        Assert.Equal(ActivityVerb.Created, act.Verb);
        Assert.Equal(owner.Id, act.ActorUserId);
        Assert.Equal("新タスク", act.Payload!.Value.GetProperty("title").GetString());
    }

    [Fact(DisplayName = "M3 更新で変更項目付きの UPDATED が記録される")]
    public async Task Update_RecordsChangedFields()
    {
        await using var ctx = _db.CreateContext();
        var (project, owner) = await TestData.CreateProjectWithOwnerAsync(ctx);
        var taskService = new TaskService(ctx);
        var activityService = new ActivityService(ctx);
        var task = (await taskService.CreateAsync(project.Id, Req("元タイトル"), owner.Id)).Data!;

        // タイトルと状態を変更
        await taskService.UpdateAsync(task.Id, Req("新タイトル", status: "IN_PROGRESS"), owner.Id);

        var activities = await activityService.GetByTaskAsync(task.Id, owner.Id);
        var updated = activities!.First(a => a.Verb == ActivityVerb.Updated);
        var fields = updated.Payload!.Value.GetProperty("fields").EnumerateArray()
            .Select(e => e.GetString()).ToList();
        Assert.Contains("タイトル", fields);
        Assert.Contains("状態", fields);
    }

    [Fact(DisplayName = "M3 変更が無ければ UPDATED は記録されない")]
    public async Task Update_NoChange_NoActivity()
    {
        await using var ctx = _db.CreateContext();
        var (project, owner) = await TestData.CreateProjectWithOwnerAsync(ctx);
        var taskService = new TaskService(ctx);
        var activityService = new ActivityService(ctx);
        var task = (await taskService.CreateAsync(project.Id, Req("変わらない"), owner.Id)).Data!;

        // 同じ値で更新(タイトルのみ既存と同一、他は未指定)
        await taskService.UpdateAsync(task.Id, Req("変わらない"), owner.Id);

        var activities = await activityService.GetByTaskAsync(task.Id, owner.Id);
        Assert.DoesNotContain(activities!, a => a.Verb == ActivityVerb.Updated);
    }

    [Fact(DisplayName = "M3 カンバン移動で MOVED(from/to) が記録される")]
    public async Task Move_RecordsMoved()
    {
        await using var ctx = _db.CreateContext();
        var (project, owner) = await TestData.CreateProjectWithOwnerAsync(ctx);
        var taskService = new TaskService(ctx);
        var activityService = new ActivityService(ctx);
        var task = (await taskService.CreateAsync(project.Id, Req("移動対象", status: "TODO"), owner.Id)).Data!;

        await taskService.MoveAsync(task.Id, new MoveTaskRequest("DONE", null), owner.Id);

        var activities = await activityService.GetByTaskAsync(task.Id, owner.Id);
        var moved = activities!.First(a => a.Verb == ActivityVerb.Moved);
        Assert.Equal("TODO", moved.Payload!.Value.GetProperty("from").GetString());
        Assert.Equal("DONE", moved.Payload!.Value.GetProperty("to").GetString());
    }

    [Fact(DisplayName = "M3 コメント投稿で COMMENTED が記録される")]
    public async Task Comment_RecordsCommented()
    {
        await using var ctx = _db.CreateContext();
        var (project, owner) = await TestData.CreateProjectWithOwnerAsync(ctx);
        var taskService = new TaskService(ctx);
        var commentService = new CommentService(ctx);
        var activityService = new ActivityService(ctx);
        var task = (await taskService.CreateAsync(project.Id, Req("コメント対象"), owner.Id)).Data!;

        await commentService.CreateAsync(task.Id, owner.Id, new CommentRequest("作業を開始します"));

        var activities = await activityService.GetByTaskAsync(task.Id, owner.Id);
        var commented = activities!.First(a => a.Verb == ActivityVerb.Commented);
        Assert.Equal("作業を開始します", commented.Payload!.Value.GetProperty("excerpt").GetString());
    }

    [Fact(DisplayName = "M3 アクティビティは新しい順に返る")]
    public async Task Get_OrdersNewestFirst()
    {
        await using var ctx = _db.CreateContext();
        var (project, owner) = await TestData.CreateProjectWithOwnerAsync(ctx);
        var taskService = new TaskService(ctx);
        var activityService = new ActivityService(ctx);
        var task = (await taskService.CreateAsync(project.Id, Req("並び順", status: "TODO"), owner.Id)).Data!;
        await taskService.MoveAsync(task.Id, new MoveTaskRequest("DONE", null), owner.Id);

        var activities = await activityService.GetByTaskAsync(task.Id, owner.Id);
        // 直近の MOVED が先頭、CREATED が後
        Assert.Equal(ActivityVerb.Moved, activities!.First().Verb);
        Assert.Equal(ActivityVerb.Created, activities!.Last().Verb);
    }

    [Fact(DisplayName = "M3 非所属ユーザーには 404 相当(null)")]
    public async Task Get_NonMember_Null()
    {
        await using var ctx = _db.CreateContext();
        var (project, owner) = await TestData.CreateProjectWithOwnerAsync(ctx);
        var outsider = await TestData.CreateUserAsync(ctx);
        var taskService = new TaskService(ctx);
        var activityService = new ActivityService(ctx);
        var task = (await taskService.CreateAsync(project.Id, Req("秘密"), owner.Id)).Data!;

        Assert.Null(await activityService.GetByTaskAsync(task.Id, outsider.Id));
    }
}
