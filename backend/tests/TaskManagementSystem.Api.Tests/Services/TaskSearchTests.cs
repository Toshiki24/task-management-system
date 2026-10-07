using TaskManagementSystem.Api.Dtos.Labels;
using TaskManagementSystem.Api.Dtos.Tasks;
using TaskManagementSystem.Api.Models;
using TaskManagementSystem.Api.Services;
using TaskManagementSystem.Api.Tests.Infrastructure;

namespace TaskManagementSystem.Api.Tests.Services;

public class TaskSearchTests : IClassFixture<TestDatabaseFixture>
{
    private readonly TestDatabaseFixture _db;

    public TaskSearchTests(TestDatabaseFixture db)
    {
        _db = db;
    }

    private static TaskRequest Req(
        string title, string? status = null, string? priority = null, long? assigneeId = null) =>
        new(assigneeId, title, null, status, priority, null);

    [Fact(DisplayName = "M2 status/priority/keyword で絞り込める")]
    public async Task Filters_ByStatusPriorityKeyword()
    {
        await using var ctx = _db.CreateContext();
        var (project, owner) = await TestData.CreateProjectWithOwnerAsync(ctx);
        var service = new TaskService(ctx);
        await service.CreateAsync(project.Id, Req("ログイン改修", "TODO", "HIGH"), owner.Id);
        await service.CreateAsync(project.Id, Req("ログアウト対応", "IN_PROGRESS", "LOW"), owner.Id);
        await service.CreateAsync(project.Id, Req("別件", "TODO", "LOW"), owner.Id);

        var byStatus = await service.GetByProjectAsync(project.Id, owner.Id, new TaskListQuery { Status = new[] { "TODO" } });
        Assert.Equal(2, byStatus!.Count);
        Assert.All(byStatus, t => Assert.Equal("TODO", t.Status));

        var byPriority = await service.GetByProjectAsync(project.Id, owner.Id, new TaskListQuery { Priority = new[] { "HIGH" } });
        Assert.Single(byPriority!);

        var byKeyword = await service.GetByProjectAsync(project.Id, owner.Id, new TaskListQuery { Keyword = "ログ" });
        Assert.Equal(2, byKeyword!.Count);
    }

    [Fact(DisplayName = "M2 assigneeId=me / none で絞り込める")]
    public async Task Filters_ByAssignee()
    {
        await using var ctx = _db.CreateContext();
        var (project, owner) = await TestData.CreateProjectWithOwnerAsync(ctx);
        var service = new TaskService(ctx);
        await service.CreateAsync(project.Id, Req("自分担当", assigneeId: owner.Id), owner.Id);
        await service.CreateAsync(project.Id, Req("未割り当て"), owner.Id);

        var mine = await service.GetByProjectAsync(project.Id, owner.Id, new TaskListQuery { AssigneeId = "me" });
        Assert.Single(mine!);
        Assert.Equal("自分担当", mine![0].Title);

        var none = await service.GetByProjectAsync(project.Id, owner.Id, new TaskListQuery { AssigneeId = "none" });
        Assert.Single(none!);
        Assert.Equal("未割り当て", none![0].Title);
    }

    [Fact(DisplayName = "M2 label で絞り込める")]
    public async Task Filters_ByLabel()
    {
        await using var ctx = _db.CreateContext();
        var admin = await TestData.CreateUserAsync(ctx);
        var ws = await TestData.CreateWorkspaceAsync(ctx);
        await TestData.AddWorkspaceMemberAsync(ctx, ws.Id, admin.Id, WorkspaceMemberRole.Admin);
        var project = await TestData.CreateProjectAsync(ctx, workspaceId: ws.Id);
        var label = (await new LabelService(ctx).CreateAsync(ws.Id, new LabelRequest("bug", null), admin.Id)).Data!;
        var service = new TaskService(ctx);
        await service.CreateAsync(project.Id, new TaskRequest(null, "ラベル付き", null, null, null, null, null, new[] { label.Id }), admin.Id);
        await service.CreateAsync(project.Id, Req("ラベルなし"), admin.Id);

        var byLabel = await service.GetByProjectAsync(project.Id, admin.Id, new TaskListQuery { LabelId = new[] { label.Id } });
        Assert.Single(byLabel!);
        Assert.Equal("ラベル付き", byLabel![0].Title);
    }

    [Fact(DisplayName = "M2 sort=title / -title で並べ替えられる")]
    public async Task Sorts_ByTitle()
    {
        await using var ctx = _db.CreateContext();
        var (project, owner) = await TestData.CreateProjectWithOwnerAsync(ctx);
        var service = new TaskService(ctx);
        await service.CreateAsync(project.Id, Req("B"), owner.Id);
        await service.CreateAsync(project.Id, Req("A"), owner.Id);
        await service.CreateAsync(project.Id, Req("C"), owner.Id);

        var asc = await service.GetByProjectAsync(project.Id, owner.Id, new TaskListQuery { Sort = "title" });
        Assert.Equal(new[] { "A", "B", "C" }, asc!.Select(t => t.Title));

        var desc = await service.GetByProjectAsync(project.Id, owner.Id, new TaskListQuery { Sort = "-title" });
        Assert.Equal(new[] { "C", "B", "A" }, desc!.Select(t => t.Title));
    }
}
