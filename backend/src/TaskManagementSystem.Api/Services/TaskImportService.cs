using Microsoft.EntityFrameworkCore;
using TaskManagementSystem.Api.Data;
using TaskManagementSystem.Api.Dtos.Metrics;
using TaskManagementSystem.Api.Dtos.Tasks;
using TaskManagementSystem.Api.Models;
using CsvReader = TaskManagementSystem.Api.Services.Csv.Csv;

namespace TaskManagementSystem.Api.Services;

/// <summary>
/// CSV からタスクを一括作成する(M5 §4)。参照/作成は CanWrite。各行を検証し、既存の
/// TaskService.CreateAsync を通して作成する(ワークフロー・ラベルの検証を再利用)。
/// 列順はエクスポートに合わせる: タイトル,状態,担当者,優先度,期限,見積,ラベル。担当者は
/// 名前が一意でないため取り込まない。すべて LINQ/EF。
/// </summary>
public class TaskImportService : ITaskImportService
{
    private const int MaxRows = 1000;

    private readonly AppDbContext _dbContext;
    private readonly ITaskService _taskService;

    private static readonly Dictionary<string, string> PriorityByLabel = new(StringComparer.OrdinalIgnoreCase)
    {
        ["高"] = TaskItemPriority.High,
        ["中"] = TaskItemPriority.Medium,
        ["低"] = TaskItemPriority.Low,
        [TaskItemPriority.High] = TaskItemPriority.High,
        [TaskItemPriority.Medium] = TaskItemPriority.Medium,
        [TaskItemPriority.Low] = TaskItemPriority.Low,
    };

    public TaskImportService(AppDbContext dbContext, ITaskService taskService)
    {
        _dbContext = dbContext;
        _taskService = taskService;
    }

    public async Task<TaskImportOutcome> ImportProjectTasksAsync(long projectId, string csv, long currentUserId)
    {
        var access = await _dbContext.ResolveAccessAsync(projectId, currentUserId);
        if (access is null || !access.Value.CanView)
        {
            return new TaskImportOutcome(TaskImportResult.ProjectNotFound);
        }

        if (!access.Value.CanWrite)
        {
            return new TaskImportOutcome(TaskImportResult.Forbidden);
        }

        var rows = CsvReader.Parse(csv);
        // 先頭が見出し行ならスキップ(エクスポート往復に対応)
        if (rows.Count > 0 && rows[0].Length > 0 && rows[0][0] == "タイトル")
        {
            rows = rows.Skip(1).ToList();
        }

        if (rows.Count > MaxRows)
        {
            return new TaskImportOutcome(TaskImportResult.TooLarge);
        }

        var workspaceId = await _dbContext.Projects
            .Where(p => p.Id == projectId).Select(p => p.WorkspaceId).FirstAsync();
        var statusKeyByName = await _dbContext.WorkflowStates
            .Where(s => s.WorkspaceId == workspaceId)
            .ToDictionaryAsync(s => s.Name, s => s.Key);
        var validStatusKeys = statusKeyByName.Values.ToHashSet();
        var labelIdByName = await _dbContext.Labels
            .Where(l => l.WorkspaceId == workspaceId)
            .ToDictionaryAsync(l => l.Name, l => l.Id);

        var imported = 0;
        var failed = new List<ImportRowErrorDto>();

        for (var i = 0; i < rows.Count; i++)
        {
            var rowNumber = i + 1;
            var cells = rows[i];

            var (request, error) = BuildRequest(cells, statusKeyByName, validStatusKeys, labelIdByName);
            if (error is not null)
            {
                failed.Add(new ImportRowErrorDto(rowNumber, error));
                continue;
            }

            var outcome = await _taskService.CreateAsync(projectId, request!, currentUserId);
            if (outcome.Result == CreateTaskResult.Success)
            {
                imported++;
            }
            else
            {
                failed.Add(new ImportRowErrorDto(rowNumber, DescribeFailure(outcome.Result)));
            }
        }

        return new TaskImportOutcome(TaskImportResult.Success, new ImportResultDto(imported, failed));
    }

    private static (TaskRequest? Request, string? Error) BuildRequest(
        string[] cells,
        IReadOnlyDictionary<string, string> statusKeyByName,
        IReadOnlySet<string> validStatusKeys,
        IReadOnlyDictionary<string, long> labelIdByName)
    {
        string Cell(int index) => index < cells.Length ? cells[index].Trim() : string.Empty;

        var title = Cell(0);
        if (string.IsNullOrWhiteSpace(title))
        {
            return (null, "タイトルは必須です。");
        }

        // 状態(表示名またはキー。空は既定状態)
        string? status = null;
        var statusText = Cell(1);
        if (!string.IsNullOrEmpty(statusText))
        {
            if (statusKeyByName.TryGetValue(statusText, out var key))
            {
                status = key;
            }
            else if (validStatusKeys.Contains(statusText))
            {
                status = statusText;
            }
            else
            {
                return (null, $"状態『{statusText}』が見つかりません。");
            }
        }

        // 優先度(日本語またはキー。空は未指定)
        string? priority = null;
        var priorityText = Cell(3);
        if (!string.IsNullOrEmpty(priorityText))
        {
            if (!PriorityByLabel.TryGetValue(priorityText, out var p))
            {
                return (null, $"優先度『{priorityText}』が不正です。");
            }
            priority = p;
        }

        // 期限(yyyy-MM-dd)
        DateOnly? dueDate = null;
        var dueText = Cell(4);
        if (!string.IsNullOrEmpty(dueText))
        {
            if (!DateOnly.TryParse(dueText, out var due))
            {
                return (null, $"期限『{dueText}』が不正です(yyyy-MM-dd)。");
            }
            dueDate = due;
        }

        // 見積(整数)
        int? estimate = null;
        var estimateText = Cell(5);
        if (!string.IsNullOrEmpty(estimateText))
        {
            if (!int.TryParse(estimateText, out var pts) || pts < 0)
            {
                return (null, $"見積『{estimateText}』が不正です。");
            }
            estimate = pts;
        }

        // ラベル(;区切りの名前。既存ラベルのみ)
        long[]? labelIds = null;
        var labelText = Cell(6);
        if (!string.IsNullOrEmpty(labelText))
        {
            var ids = new List<long>();
            foreach (var name in labelText.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (!labelIdByName.TryGetValue(name, out var id))
                {
                    return (null, $"ラベル『{name}』が見つかりません。");
                }
                ids.Add(id);
            }
            labelIds = ids.ToArray();
        }

        return (new TaskRequest(
            AssigneeId: null,
            Title: title,
            Description: null,
            Status: status,
            Priority: priority,
            DueDate: dueDate,
            EstimatePoints: estimate,
            LabelIds: labelIds,
            ParentTaskId: null), null);
    }

    private static string DescribeFailure(CreateTaskResult result) => result switch
    {
        CreateTaskResult.InvalidStatus => "タスク状態の値が不正です。",
        CreateTaskResult.InvalidLabel => "指定されたラベルが存在しません。",
        _ => "タスクを作成できませんでした。",
    };
}
