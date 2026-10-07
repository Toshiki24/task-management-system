using Microsoft.EntityFrameworkCore;
using TaskManagementSystem.Api.Dtos.Tasks;
using TaskManagementSystem.Api.Models;
using TaskManagementSystem.Api.Services;
using TaskManagementSystem.Api.Tests.Infrastructure;

namespace TaskManagementSystem.Api.Tests.Services;

public class SubtaskChecklistTests : IClassFixture<TestDatabaseFixture>
{
    private readonly TestDatabaseFixture _db;

    public SubtaskChecklistTests(TestDatabaseFixture db)
    {
        _db = db;
    }

    private static TaskRequest Req(string title, long? parentId = null, string? status = null) =>
        new(null, title, null, status, null, null, null, null, parentId);

    [Fact(DisplayName = "M2 サブタスクを作成でき、親の進捗に反映される")]
    public async Task CreateSubtask_ReflectsParentProgress()
    {
        await using var ctx = _db.CreateContext();
        var (project, owner) = await TestData.CreateProjectWithOwnerAsync(ctx);
        var service = new TaskService(ctx);

        var parent = (await service.CreateAsync(project.Id, Req("親"), owner.Id)).Data!;
        var child1 = await service.CreateAsync(project.Id, Req("子1", parent.Id, "DONE"), owner.Id);
        var child2 = await service.CreateAsync(project.Id, Req("子2", parent.Id, "TODO"), owner.Id);

        Assert.Equal(CreateTaskResult.Success, child1.Result);
        Assert.Equal(parent.Id, child1.Data!.ParentTaskId);

        // 親を取得すると 1/2 完了(DONE はカテゴリ完了)
        var reloadedParent = await service.GetByIdAsync(parent.Id, owner.Id);
        Assert.Equal(1, reloadedParent!.SubtaskProgress.Done);
        Assert.Equal(2, reloadedParent.SubtaskProgress.Total);
        Assert.Equal(CreateTaskResult.Success, child2.Result);
    }

    [Fact(DisplayName = "M2 別プロジェクトの親、サブタスクの親(2階層)は InvalidParent")]
    public async Task CreateSubtask_RejectsInvalidParent()
    {
        await using var ctx = _db.CreateContext();
        var (project, owner) = await TestData.CreateProjectWithOwnerAsync(ctx);
        var service = new TaskService(ctx);

        // 別プロジェクトの親
        var otherProject = await TestData.CreateProjectAsync(ctx);
        var foreignParent = await TestData.CreateTaskAsync(ctx, otherProject.Id);
        Assert.Equal(CreateTaskResult.InvalidParent,
            (await service.CreateAsync(project.Id, Req("子", foreignParent.Id), owner.Id)).Result);

        // サブタスクを親に指定(2 階層目は不可)
        var parent = (await service.CreateAsync(project.Id, Req("親"), owner.Id)).Data!;
        var child = (await service.CreateAsync(project.Id, Req("子", parent.Id), owner.Id)).Data!;
        Assert.Equal(CreateTaskResult.InvalidParent,
            (await service.CreateAsync(project.Id, Req("孫", child.Id), owner.Id)).Result);
    }

    [Fact(DisplayName = "M2 親タスク削除で子タスクも連動削除される")]
    public async Task DeleteParent_CascadesSubtasks()
    {
        await using var ctx = _db.CreateContext();
        var (project, owner) = await TestData.CreateProjectWithOwnerAsync(ctx);
        var service = new TaskService(ctx);
        var parent = (await service.CreateAsync(project.Id, Req("親"), owner.Id)).Data!;
        var child = (await service.CreateAsync(project.Id, Req("子", parent.Id), owner.Id)).Data!;

        Assert.Equal(DeleteTaskResult.Success, await service.DeleteAsync(parent.Id, owner.Id));
        Assert.False(await ctx.Tasks.AnyAsync(t => t.Id == child.Id));
    }

    [Fact(DisplayName = "M2 チェックリストの作成・完了トグル・削除ができる")]
    public async Task Checklist_CrudAndToggle()
    {
        await using var ctx = _db.CreateContext();
        var (project, owner) = await TestData.CreateProjectWithOwnerAsync(ctx);
        var task = await TestData.CreateTaskAsync(ctx, project.Id);
        var service = new ChecklistService(ctx);

        var created = await service.CreateAsync(task.Id, new ChecklistItemRequest("手順1"), owner.Id);
        Assert.Equal(CreateChecklistItemResult.Success, created.Result);
        Assert.False(created.Data!.IsDone);

        var updated = await service.UpdateAsync(task.Id, created.Data.Id, new ChecklistItemRequest("手順1", true), owner.Id);
        Assert.Equal(UpdateChecklistItemResult.Success, updated.Result);
        Assert.True(updated.Data!.IsDone);

        var list = await service.GetByTaskAsync(task.Id, owner.Id);
        Assert.Single(list!);

        Assert.Equal(DeleteChecklistItemResult.Success, await service.DeleteAsync(task.Id, created.Data.Id, owner.Id));
        Assert.Empty((await service.GetByTaskAsync(task.Id, owner.Id))!);
    }

    [Fact(DisplayName = "M2 Viewer はチェックリストを変更できない(403 相当)")]
    public async Task Checklist_ViewerCannotWrite()
    {
        await using var ctx = _db.CreateContext();
        var (project, owner) = await TestData.CreateProjectWithOwnerAsync(ctx);
        var viewer = await TestData.CreateUserAsync(ctx);
        var workspaceId = await ctx.Projects.Where(p => p.Id == project.Id).Select(p => p.WorkspaceId).SingleAsync();
        await TestData.AddWorkspaceMemberAsync(ctx, workspaceId, viewer.Id, WorkspaceMemberRole.Viewer);
        var task = await TestData.CreateTaskAsync(ctx, project.Id);

        Assert.Equal(CreateChecklistItemResult.Forbidden,
            (await new ChecklistService(ctx).CreateAsync(task.Id, new ChecklistItemRequest("x"), viewer.Id)).Result);
    }
}
