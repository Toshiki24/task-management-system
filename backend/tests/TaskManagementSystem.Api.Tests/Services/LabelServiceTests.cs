using Microsoft.EntityFrameworkCore;
using TaskManagementSystem.Api.Dtos.Labels;
using TaskManagementSystem.Api.Dtos.Tasks;
using TaskManagementSystem.Api.Models;
using TaskManagementSystem.Api.Services;
using TaskManagementSystem.Api.Tests.Infrastructure;

namespace TaskManagementSystem.Api.Tests.Services;

public class LabelServiceTests : IClassFixture<TestDatabaseFixture>
{
    private readonly TestDatabaseFixture _db;

    public LabelServiceTests(TestDatabaseFixture db)
    {
        _db = db;
    }

    [Fact(DisplayName = "M2 ラベル一覧は非所属ユーザーには null(= 404)")]
    public async Task GetByWorkspace_NullForNonMember()
    {
        await using var ctx = _db.CreateContext();
        var outsider = await TestData.CreateUserAsync(ctx);
        var ws = await TestData.CreateWorkspaceAsync(ctx);

        Assert.Null(await new LabelService(ctx).GetByWorkspaceAsync(ws.Id, outsider.Id));
    }

    [Fact(DisplayName = "M2 ラベル作成は WS Admin のみ。Member は 403、重複名は 409")]
    public async Task Create_AdminOnly_AndUnique()
    {
        await using var ctx = _db.CreateContext();
        var admin = await TestData.CreateUserAsync(ctx);
        var member = await TestData.CreateUserAsync(ctx);
        var ws = await TestData.CreateWorkspaceAsync(ctx);
        await TestData.AddWorkspaceMemberAsync(ctx, ws.Id, admin.Id, WorkspaceMemberRole.Admin);
        await TestData.AddWorkspaceMemberAsync(ctx, ws.Id, member.Id, WorkspaceMemberRole.Member);
        var service = new LabelService(ctx);

        Assert.Equal(CreateLabelResult.Forbidden,
            (await service.CreateAsync(ws.Id, new LabelRequest("bug", "#f00"), member.Id)).Result);

        Assert.Equal(CreateLabelResult.Success,
            (await service.CreateAsync(ws.Id, new LabelRequest("bug", "#f00"), admin.Id)).Result);

        Assert.Equal(CreateLabelResult.DuplicateName,
            (await service.CreateAsync(ws.Id, new LabelRequest("bug", "#00f"), admin.Id)).Result);
    }

    [Fact(DisplayName = "M2 ラベル削除でタスクへの付与(task_labels)も連動削除される")]
    public async Task Delete_CascadesTaskLabels()
    {
        await using var ctx = _db.CreateContext();
        var admin = await TestData.CreateUserAsync(ctx);
        var ws = await TestData.CreateWorkspaceAsync(ctx);
        await TestData.AddWorkspaceMemberAsync(ctx, ws.Id, admin.Id, WorkspaceMemberRole.Admin);
        var project = await TestData.CreateProjectAsync(ctx, workspaceId: ws.Id);
        var labelService = new LabelService(ctx);

        var label = (await labelService.CreateAsync(ws.Id, new LabelRequest("urgent", null), admin.Id)).Data!;
        var created = await new TaskService(ctx).CreateAsync(
            project.Id, new TaskRequest(null, "ラベル付き", null, null, null, null, null, new[] { label.Id }), admin.Id);
        Assert.Equal(CreateTaskResult.Success, created.Result);
        Assert.Single(created.Data!.Labels);

        Assert.Equal(DeleteLabelResult.Success, await labelService.DeleteAsync(ws.Id, label.Id, admin.Id));

        Assert.False(await ctx.TaskLabels.AnyAsync(tl => tl.LabelId == label.Id));
    }

    [Fact(DisplayName = "M2 他WSのラベルIDをタスクに付与しようとすると InvalidLabel")]
    public async Task CreateTask_RejectsForeignLabel()
    {
        await using var ctx = _db.CreateContext();
        var (project, owner) = await TestData.CreateProjectWithOwnerAsync(ctx);
        // 別WSのラベル
        var otherWs = await TestData.CreateWorkspaceAsync(ctx);
        var otherLabel = new Label { WorkspaceId = otherWs.Id, Name = "foreign" };
        ctx.Labels.Add(otherLabel);
        await ctx.SaveChangesAsync();

        var outcome = await new TaskService(ctx).CreateAsync(
            project.Id, new TaskRequest(null, "T", null, null, null, null, null, new[] { otherLabel.Id }), owner.Id);

        Assert.Equal(CreateTaskResult.InvalidLabel, outcome.Result);
    }

    [Fact(DisplayName = "M2 タスク更新でラベルを完全置換・見積を設定できる")]
    public async Task UpdateTask_SetsLabelsAndEstimate()
    {
        await using var ctx = _db.CreateContext();
        var admin = await TestData.CreateUserAsync(ctx);
        var ws = await TestData.CreateWorkspaceAsync(ctx);
        await TestData.AddWorkspaceMemberAsync(ctx, ws.Id, admin.Id, WorkspaceMemberRole.Admin);
        var project = await TestData.CreateProjectAsync(ctx, workspaceId: ws.Id);
        var labelService = new LabelService(ctx);
        var taskService = new TaskService(ctx);

        var a = (await labelService.CreateAsync(ws.Id, new LabelRequest("A", null), admin.Id)).Data!;
        var b = (await labelService.CreateAsync(ws.Id, new LabelRequest("B", null), admin.Id)).Data!;
        var task = (await taskService.CreateAsync(
            project.Id, new TaskRequest(null, "T", null, null, null, null, 5, new[] { a.Id }), admin.Id)).Data!;
        Assert.Equal(5, task.EstimatePoints);
        Assert.Single(task.Labels);

        // A→B に置換、見積を 8 に
        var updated = await taskService.UpdateAsync(
            task.Id, new TaskRequest(null, "T", null, null, null, null, 8, new[] { b.Id }), admin.Id);

        Assert.Equal(UpdateTaskResult.Success, updated.Result);
        Assert.Equal(8, updated.Data!.EstimatePoints);
        Assert.Equal(new[] { "B" }, updated.Data.Labels.Select(l => l.Name));
    }
}
