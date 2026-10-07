using TaskManagementSystem.Api.Dtos.Tasks;
using TaskManagementSystem.Api.Models;
using TaskManagementSystem.Api.Services;
using TaskManagementSystem.Api.Tests.Infrastructure;

namespace TaskManagementSystem.Api.Tests.Services;

public class MyTasksTests : IClassFixture<TestDatabaseFixture>
{
    private readonly TestDatabaseFixture _db;

    public MyTasksTests(TestDatabaseFixture db)
    {
        _db = db;
    }

    [Fact(DisplayName = "M2 My Tasks は所属する全WSの自分の担当のみを横断で返す")]
    public async Task GetMyTasks_CrossWorkspace_OnlyMineAndVisible()
    {
        await using var ctx = _db.CreateContext();
        var me = await TestData.CreateUserAsync(ctx);
        var other = await TestData.CreateUserAsync(ctx);

        // WS-A(me=Member), WS-B(me=Member) にそれぞれ自分担当タスク
        var wsA = await TestData.CreateWorkspaceAsync(ctx);
        await TestData.AddWorkspaceMemberAsync(ctx, wsA.Id, me.Id, WorkspaceMemberRole.Member);
        var projA = await TestData.CreateProjectAsync(ctx, workspaceId: wsA.Id);
        await TestData.AddMemberAsync(ctx, projA.Id, me.Id);
        var ta = await TestData.CreateTaskAsync(ctx, projA.Id, me.Id);

        var wsB = await TestData.CreateWorkspaceAsync(ctx);
        await TestData.AddWorkspaceMemberAsync(ctx, wsB.Id, me.Id, WorkspaceMemberRole.Member);
        var projB = await TestData.CreateProjectAsync(ctx, workspaceId: wsB.Id);
        await TestData.AddMemberAsync(ctx, projB.Id, me.Id);
        var tb = await TestData.CreateTaskAsync(ctx, projB.Id, me.Id);

        // 他人担当(返らない)と、me が非所属のWSのタスク(返らない)
        await TestData.CreateTaskAsync(ctx, projA.Id, other.Id);
        var wsC = await TestData.CreateWorkspaceAsync(ctx);
        var projC = await TestData.CreateProjectAsync(ctx, workspaceId: wsC.Id);
        await TestData.CreateTaskAsync(ctx, projC.Id, me.Id); // me は wsC 非所属

        var result = await new TaskService(ctx).GetMyTasksAsync(me.Id, new MyTasksQuery());

        Assert.Equal(2, result.Total);
        Assert.Equal(new[] { ta.Id, tb.Id }.OrderBy(x => x), result.Items.Select(i => i.Id).OrderBy(x => x));
        var itemA = result.Items.Single(i => i.Id == ta.Id);
        Assert.Equal(wsA.Id, itemA.WorkspaceId);
        Assert.Equal(projA.Name, itemA.ProjectName);
    }

    [Fact(DisplayName = "M2 My Tasks はページングする")]
    public async Task GetMyTasks_Paginates()
    {
        await using var ctx = _db.CreateContext();
        var me = await TestData.CreateUserAsync(ctx);
        var ws = await TestData.CreateWorkspaceAsync(ctx);
        await TestData.AddWorkspaceMemberAsync(ctx, ws.Id, me.Id, WorkspaceMemberRole.Member);
        var project = await TestData.CreateProjectAsync(ctx, workspaceId: ws.Id);
        await TestData.AddMemberAsync(ctx, project.Id, me.Id);
        for (var i = 0; i < 3; i++)
        {
            await TestData.CreateTaskAsync(ctx, project.Id, me.Id);
        }

        var page1 = await new TaskService(ctx).GetMyTasksAsync(me.Id, new MyTasksQuery { Page = 1, PageSize = 2 });
        Assert.Equal(3, page1.Total);
        Assert.Equal(2, page1.Items.Count);

        var page2 = await new TaskService(ctx).GetMyTasksAsync(me.Id, new MyTasksQuery { Page = 2, PageSize = 2 });
        Assert.Single(page2.Items);
    }

    [Fact(DisplayName = "M2 My Tasks は status で絞り込める")]
    public async Task GetMyTasks_FiltersByStatus()
    {
        await using var ctx = _db.CreateContext();
        var me = await TestData.CreateUserAsync(ctx);
        var ws = await TestData.CreateWorkspaceAsync(ctx);
        await TestData.AddWorkspaceMemberAsync(ctx, ws.Id, me.Id, WorkspaceMemberRole.Member);
        var project = await TestData.CreateProjectAsync(ctx, workspaceId: ws.Id);
        await TestData.AddMemberAsync(ctx, project.Id, me.Id);
        var service = new TaskService(ctx);
        await service.CreateAsync(project.Id, new TaskRequest(me.Id, "T1", null, "TODO", null, null), me.Id);
        await service.CreateAsync(project.Id, new TaskRequest(me.Id, "T2", null, "IN_PROGRESS", null, null), me.Id);

        var result = await service.GetMyTasksAsync(me.Id, new MyTasksQuery { Status = new[] { "IN_PROGRESS" } });

        Assert.Equal(1, result.Total);
        Assert.Equal("IN_PROGRESS", result.Items[0].Status);
    }
}
