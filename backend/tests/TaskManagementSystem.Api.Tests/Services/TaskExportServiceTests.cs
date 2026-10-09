using System.Text;
using TaskManagementSystem.Api.Models;
using TaskManagementSystem.Api.Services;
using TaskManagementSystem.Api.Services.Csv;
using TaskManagementSystem.Api.Tests.Infrastructure;

namespace TaskManagementSystem.Api.Tests.Services;

public class TaskExportServiceTests : IClassFixture<TestDatabaseFixture>
{
    private readonly TestDatabaseFixture _db;

    public TaskExportServiceTests(TestDatabaseFixture db)
    {
        _db = db;
    }

    [Theory(DisplayName = "M5 CSV: 数式になりうる先頭文字を無害化する")]
    [InlineData("=1+1", "'=1+1")]
    [InlineData("+80", "'+80")]
    [InlineData("-5", "'-5")]
    [InlineData("@cmd", "'@cmd")]
    [InlineData("普通のタイトル", "普通のタイトル")]
    public void Csv_EscapesFormulaInjection(string input, string expected)
    {
        Assert.Equal(expected, Csv.EscapeCell(input));
    }

    [Fact(DisplayName = "M5 CSV: カンマ・引用符・改行を含むセルは引用する")]
    public void Csv_QuotesSpecialChars()
    {
        Assert.Equal("\"a,b\"", Csv.EscapeCell("a,b"));
        Assert.Equal("\"a\"\"b\"", Csv.EscapeCell("a\"b"));
        Assert.Equal("\"a\nb\"", Csv.EscapeCell("a\nb"));
    }

    [Fact(DisplayName = "M5 エクスポート: ヘッダ＋タスク行を出力し、BOM 付き")]
    public async Task Export_ProducesCsv()
    {
        await using var ctx = _db.CreateContext();
        var (project, owner) = await TestData.CreateProjectWithOwnerAsync(ctx);
        ctx.Tasks.Add(new TaskItem
        {
            ProjectId = project.Id, Title = "ログイン不具合", Status = "DONE",
            Priority = TaskItemPriority.High, AssigneeId = owner.Id,
            DueDate = new DateOnly(2026, 3, 1), EstimatePoints = 5,
        });
        await ctx.SaveChangesAsync();

        var bytes = await new TaskExportService(ctx).ExportProjectTasksCsvAsync(project.Id, owner.Id);

        Assert.NotNull(bytes);
        // 先頭に UTF-8 BOM
        Assert.Equal(Encoding.UTF8.GetPreamble(), bytes!.Take(3).ToArray());
        var text = Encoding.UTF8.GetString(bytes);
        Assert.Contains("タイトル,状態,担当者,優先度,期限,見積,ラベル", text);
        Assert.Contains("ログイン不具合", text);
        Assert.Contains("完了", text);   // 状態の表示名
        Assert.Contains("高", text);      // 優先度の日本語
        Assert.Contains("2026-03-01", text);
    }

    [Fact(DisplayName = "M5 エクスポート: 非所属ユーザーには null")]
    public async Task Export_NonMember_Null()
    {
        await using var ctx = _db.CreateContext();
        var (project, _) = await TestData.CreateProjectWithOwnerAsync(ctx);
        var outsider = await TestData.CreateUserAsync(ctx);

        Assert.Null(await new TaskExportService(ctx).ExportProjectTasksCsvAsync(project.Id, outsider.Id));
    }
}
