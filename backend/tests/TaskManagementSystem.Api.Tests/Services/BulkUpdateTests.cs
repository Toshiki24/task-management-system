using Microsoft.EntityFrameworkCore;
using TaskManagementSystem.Api.Dtos.Tasks;
using TaskManagementSystem.Api.Models;
using TaskManagementSystem.Api.Services;
using TaskManagementSystem.Api.Tests.Infrastructure;

namespace TaskManagementSystem.Api.Tests.Services;

public class BulkUpdateTests : IClassFixture<TestDatabaseFixture>
{
    private readonly TestDatabaseFixture _db;

    public BulkUpdateTests(TestDatabaseFixture db)
    {
        _db = db;
    }

    private static async Task<Label> AddLabelAsync(TaskManagementSystem.Api.Data.AppDbContext ctx, long workspaceId)
    {
        var label = new Label { WorkspaceId = workspaceId, Name = TestData.Unique("ラベル"), Color = "#4f46e5" };
        ctx.Labels.Add(label);
        await ctx.SaveChangesAsync();
        return label;
    }

    private static async Task<long> WorkspaceOf(TaskManagementSystem.Api.Data.AppDbContext ctx, long projectId) =>
        await ctx.Projects.Where(p => p.Id == projectId).Select(p => p.WorkspaceId).SingleAsync();

    [Fact(DisplayName = "M2 一括で状態を変更でき、履歴も記録される")]
    public async Task Bulk_ChangeStatus_AppliesAndRecordsHistory()
    {
        await using var ctx = _db.CreateContext();
        var (project, owner) = await TestData.CreateProjectWithOwnerAsync(ctx);
        var service = new TaskService(ctx);
        var t1 = await TestData.CreateTaskAsync(ctx, project.Id);
        var t2 = await TestData.CreateTaskAsync(ctx, project.Id);

        var outcome = await service.BulkUpdateAsync(
            project.Id, new BulkUpdateTasksRequest(new[] { t1.Id, t2.Id }, Status: "DONE"), owner.Id);

        Assert.Equal(BulkUpdateResult.Success, outcome.Result);
        Assert.Equal(2, outcome.Updated);
        var statuses = await ctx.Tasks.Where(t => t.Id == t1.Id || t.Id == t2.Id).Select(t => t.Status).ToListAsync();
        Assert.All(statuses, s => Assert.Equal("DONE", s));
        Assert.Equal(2, await ctx.TaskStatusHistories.CountAsync(h => h.ToStatus == "DONE" && (h.TaskId == t1.Id || h.TaskId == t2.Id)));
    }

    [Fact(DisplayName = "M2 一括で担当を設定・未割り当てにできる。SetAssignee=false は変更しない")]
    public async Task Bulk_SetAssignee_AppliesOnlyWhenRequested()
    {
        await using var ctx = _db.CreateContext();
        var (project, owner) = await TestData.CreateProjectWithOwnerAsync(ctx);
        var member = await TestData.CreateUserAsync(ctx);
        await TestData.AddMemberAsync(ctx, project.Id, member.Id, ProjectMemberRole.Member);
        var service = new TaskService(ctx);
        var t1 = await TestData.CreateTaskAsync(ctx, project.Id, assigneeId: owner.Id);

        // 担当を member に変更
        Assert.Equal(BulkUpdateResult.Success,
            (await service.BulkUpdateAsync(project.Id,
                new BulkUpdateTasksRequest(new[] { t1.Id }, SetAssignee: true, AssigneeId: member.Id), owner.Id)).Result);
        Assert.Equal(member.Id, (await ctx.Tasks.FindAsync(t1.Id))!.AssigneeId);

        // SetAssignee=false は担当を変えない
        await service.BulkUpdateAsync(project.Id, new BulkUpdateTasksRequest(new[] { t1.Id }, Status: "IN_PROGRESS"), owner.Id);
        ctx.ChangeTracker.Clear();
        Assert.Equal(member.Id, (await ctx.Tasks.FindAsync(t1.Id))!.AssigneeId);

        // 未割り当てにする
        await service.BulkUpdateAsync(project.Id,
            new BulkUpdateTasksRequest(new[] { t1.Id }, SetAssignee: true, AssigneeId: null), owner.Id);
        ctx.ChangeTracker.Clear();
        Assert.Null((await ctx.Tasks.FindAsync(t1.Id))!.AssigneeId);
    }

    [Fact(DisplayName = "M2 一括でラベルを付与・除去できる(加減算)")]
    public async Task Bulk_AddAndRemoveLabels()
    {
        await using var ctx = _db.CreateContext();
        var (project, owner) = await TestData.CreateProjectWithOwnerAsync(ctx);
        var workspaceId = await WorkspaceOf(ctx, project.Id);
        var service = new TaskService(ctx);
        var label1 = await AddLabelAsync(ctx, workspaceId);
        var label2 = await AddLabelAsync(ctx, workspaceId);
        var t1 = await TestData.CreateTaskAsync(ctx, project.Id);
        var t2 = await TestData.CreateTaskAsync(ctx, project.Id);

        // label1, label2 を付与
        await service.BulkUpdateAsync(project.Id,
            new BulkUpdateTasksRequest(new[] { t1.Id, t2.Id }, AddLabelIds: new[] { label1.Id, label2.Id }), owner.Id);
        Assert.Equal(4, await ctx.TaskLabels.CountAsync(tl => tl.TaskId == t1.Id || tl.TaskId == t2.Id));

        // 二重付与はしない + label1 を除去
        await service.BulkUpdateAsync(project.Id,
            new BulkUpdateTasksRequest(new[] { t1.Id, t2.Id }, AddLabelIds: new[] { label2.Id }, RemoveLabelIds: new[] { label1.Id }),
            owner.Id);
        var remaining = await ctx.TaskLabels.Where(tl => tl.TaskId == t1.Id || tl.TaskId == t2.Id).Select(tl => tl.LabelId).ToListAsync();
        Assert.All(remaining, id => Assert.Equal(label2.Id, id));
        Assert.Equal(2, remaining.Count);
    }

    [Fact(DisplayName = "M2 本プロジェクト外・存在しないタスク ID が混ざると何も変更しない(InvalidTask)")]
    public async Task Bulk_ForeignTask_RejectsAll()
    {
        await using var ctx = _db.CreateContext();
        var (project, owner) = await TestData.CreateProjectWithOwnerAsync(ctx);
        var service = new TaskService(ctx);
        var t1 = await TestData.CreateTaskAsync(ctx, project.Id);
        var otherProject = await TestData.CreateProjectAsync(ctx);
        var foreign = await TestData.CreateTaskAsync(ctx, otherProject.Id);

        var outcome = await service.BulkUpdateAsync(
            project.Id, new BulkUpdateTasksRequest(new[] { t1.Id, foreign.Id }, Status: "DONE"), owner.Id);

        Assert.Equal(BulkUpdateResult.InvalidTask, outcome.Result);
        // t1 も変更されていないこと
        Assert.NotEqual("DONE", (await ctx.Tasks.FindAsync(t1.Id))!.Status);
    }

    [Fact(DisplayName = "M2 不正な状態・ラベル・担当は検証で弾く")]
    public async Task Bulk_Validations()
    {
        await using var ctx = _db.CreateContext();
        var (project, owner) = await TestData.CreateProjectWithOwnerAsync(ctx);
        var service = new TaskService(ctx);
        var t1 = await TestData.CreateTaskAsync(ctx, project.Id);

        Assert.Equal(BulkUpdateResult.InvalidStatus,
            (await service.BulkUpdateAsync(project.Id, new BulkUpdateTasksRequest(new[] { t1.Id }, Status: "NOPE"), owner.Id)).Result);

        Assert.Equal(BulkUpdateResult.InvalidLabel,
            (await service.BulkUpdateAsync(project.Id, new BulkUpdateTasksRequest(new[] { t1.Id }, AddLabelIds: new[] { 999999L }), owner.Id)).Result);

        var stranger = await TestData.CreateUserAsync(ctx);
        Assert.Equal(BulkUpdateResult.AssigneeNotMember,
            (await service.BulkUpdateAsync(project.Id,
                new BulkUpdateTasksRequest(new[] { t1.Id }, SetAssignee: true, AssigneeId: stranger.Id), owner.Id)).Result);
    }

    [Fact(DisplayName = "M2 Viewer は一括更新できない(403 相当)")]
    public async Task Bulk_Viewer_Forbidden()
    {
        await using var ctx = _db.CreateContext();
        var (project, _) = await TestData.CreateProjectWithOwnerAsync(ctx);
        var viewer = await TestData.CreateUserAsync(ctx);
        var workspaceId = await WorkspaceOf(ctx, project.Id);
        await TestData.AddWorkspaceMemberAsync(ctx, workspaceId, viewer.Id, WorkspaceMemberRole.Viewer);
        var service = new TaskService(ctx);
        var t1 = await TestData.CreateTaskAsync(ctx, project.Id);

        Assert.Equal(BulkUpdateResult.Forbidden,
            (await service.BulkUpdateAsync(project.Id, new BulkUpdateTasksRequest(new[] { t1.Id }, Status: "DONE"), viewer.Id)).Result);
    }

    [Fact(DisplayName = "M2 非所属ユーザーには 404 相当(存在を開示しない)")]
    public async Task Bulk_NonMember_ProjectNotFound()
    {
        await using var ctx = _db.CreateContext();
        var (project, _) = await TestData.CreateProjectWithOwnerAsync(ctx);
        var outsider = await TestData.CreateUserAsync(ctx);
        var service = new TaskService(ctx);
        var t1 = await TestData.CreateTaskAsync(ctx, project.Id);

        Assert.Equal(BulkUpdateResult.ProjectNotFound,
            (await service.BulkUpdateAsync(project.Id, new BulkUpdateTasksRequest(new[] { t1.Id }, Status: "DONE"), outsider.Id)).Result);
    }
}
