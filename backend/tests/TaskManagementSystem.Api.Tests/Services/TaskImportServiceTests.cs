using Microsoft.EntityFrameworkCore;
using TaskManagementSystem.Api.Models;
using TaskManagementSystem.Api.Services;
using TaskManagementSystem.Api.Services.Csv;
using TaskManagementSystem.Api.Tests.Infrastructure;

namespace TaskManagementSystem.Api.Tests.Services;

public class TaskImportServiceTests : IClassFixture<TestDatabaseFixture>
{
    private readonly TestDatabaseFixture _db;

    public TaskImportServiceTests(TestDatabaseFixture db)
    {
        _db = db;
    }

    private static TaskImportService NewService(TaskManagementSystem.Api.Data.AppDbContext ctx) =>
        new(ctx, new TaskService(ctx));

    private const string Header = "タイトル,状態,担当者,優先度,期限,見積,ラベル\r\n";

    [Fact(DisplayName = "M5 CSV解析: 引用・二重引用符・改行・BOM を正しく扱う")]
    public void Csv_Parse()
    {
        var content = "﻿タイトル,状態\r\n\"a,b\",\"行\n跨ぎ\"\r\n\"q\"\"q\",x\r\n";
        var rows = Csv.Parse(content);

        Assert.Equal(3, rows.Count);
        Assert.Equal(new[] { "タイトル", "状態" }, rows[0]);
        Assert.Equal(new[] { "a,b", "行\n跨ぎ" }, rows[1]);
        Assert.Equal(new[] { "q\"q", "x" }, rows[2]);
    }

    [Fact(DisplayName = "M5 インポート: ヘッダ付き CSV から有効行を取り込む")]
    public async Task Import_ValidRows()
    {
        await using var ctx = _db.CreateContext();
        var (project, owner) = await TestData.CreateProjectWithOwnerAsync(ctx);
        var csv = Header
            + "新規タスクA,対応中,,高,2026-03-01,5,\r\n"
            + "新規タスクB,,,,,, \r\n";

        var outcome = await NewService(ctx).ImportProjectTasksAsync(project.Id, csv, owner.Id);

        Assert.Equal(TaskImportResult.Success, outcome.Result);
        Assert.Equal(2, outcome.Data!.Imported);
        Assert.Empty(outcome.Data.Failed);
        var a = await ctx.Tasks.FirstAsync(t => t.Title == "新規タスクA");
        Assert.Equal("IN_PROGRESS", a.Status);
        Assert.Equal(TaskItemPriority.High, a.Priority);
        Assert.Equal(new DateOnly(2026, 3, 1), a.DueDate);
        Assert.Equal(5, a.EstimatePoints);
    }

    [Fact(DisplayName = "M5 インポート: 行単位でエラーを報告し、他の行は取り込む")]
    public async Task Import_RowErrors()
    {
        await using var ctx = _db.CreateContext();
        var (project, owner) = await TestData.CreateProjectWithOwnerAsync(ctx);
        var csv = Header
            + ",対応中,,,,,\r\n"              // 1行目: タイトル無し
            + "状態不正,ないない,,,,,\r\n"      // 2行目: 状態が不正
            + "正常タスク,対応中,,,,,\r\n";     // 3行目: OK

        var outcome = await NewService(ctx).ImportProjectTasksAsync(project.Id, csv, owner.Id);

        Assert.Equal(1, outcome.Data!.Imported);
        Assert.Equal(2, outcome.Data.Failed.Count);
        Assert.Equal(1, outcome.Data.Failed[0].Row);
        Assert.Contains("タイトル", outcome.Data.Failed[0].Message);
        Assert.Equal(2, outcome.Data.Failed[1].Row);
        Assert.Contains("状態", outcome.Data.Failed[1].Message);
    }

    [Fact(DisplayName = "M5 インポート: 既存ラベル名を解決し付与する/未知は行エラー")]
    public async Task Import_Labels()
    {
        await using var ctx = _db.CreateContext();
        var (project, owner) = await TestData.CreateProjectWithOwnerAsync(ctx);
        ctx.Labels.Add(new Label { WorkspaceId = project.WorkspaceId, Name = "bug" });
        await ctx.SaveChangesAsync();
        var csv = Header
            + "ラベル付き,対応中,,,,,bug\r\n"
            + "未知ラベル,対応中,,,,,unknown\r\n";

        var outcome = await NewService(ctx).ImportProjectTasksAsync(project.Id, csv, owner.Id);

        Assert.Equal(1, outcome.Data!.Imported);
        Assert.Single(outcome.Data.Failed);
        Assert.Contains("ラベル", outcome.Data.Failed[0].Message);
        var task = await ctx.Tasks.Include(t => t.TaskLabels).FirstAsync(t => t.Title == "ラベル付き");
        Assert.Single(task.TaskLabels);
    }

    [Fact(DisplayName = "M5 インポート: Viewer はできない(Forbidden)")]
    public async Task Import_Viewer_Forbidden()
    {
        await using var ctx = _db.CreateContext();
        var (project, _) = await TestData.CreateProjectWithOwnerAsync(ctx);
        var viewer = await TestData.CreateUserAsync(ctx);
        await TestData.AddWorkspaceMemberAsync(ctx, project.WorkspaceId, viewer.Id, WorkspaceMemberRole.Viewer);

        var outcome = await NewService(ctx).ImportProjectTasksAsync(project.Id, Header + "x,,,,,,\r\n", viewer.Id);
        Assert.Equal(TaskImportResult.Forbidden, outcome.Result);
    }

    [Fact(DisplayName = "M5 インポート: 非所属ユーザーには ProjectNotFound")]
    public async Task Import_NonMember_NotFound()
    {
        await using var ctx = _db.CreateContext();
        var (project, _) = await TestData.CreateProjectWithOwnerAsync(ctx);
        var outsider = await TestData.CreateUserAsync(ctx);

        var outcome = await NewService(ctx).ImportProjectTasksAsync(project.Id, Header, outsider.Id);
        Assert.Equal(TaskImportResult.ProjectNotFound, outcome.Result);
    }
}
