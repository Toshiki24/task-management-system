using System.Text;
using Microsoft.EntityFrameworkCore;
using TaskManagementSystem.Api.Data;
using TaskManagementSystem.Api.Models;
using CsvWriter = TaskManagementSystem.Api.Services.Csv.Csv;

namespace TaskManagementSystem.Api.Services;

/// <summary>
/// プロジェクトのタスクを CSV へ出力する(M5 §4)。参照は CanView。状態は WS のワークフロー表示名、
/// 優先度は日本語、ラベルは「;」区切り。CSV インジェクション対策は Csv ユーティリティで行う。
/// すべて LINQ/EF。
/// </summary>
public class TaskExportService : ITaskExportService
{
    private readonly AppDbContext _dbContext;

    private static readonly Dictionary<string, string> PriorityLabels = new()
    {
        [TaskItemPriority.High] = "高",
        [TaskItemPriority.Medium] = "中",
        [TaskItemPriority.Low] = "低",
    };

    public TaskExportService(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<byte[]?> ExportProjectTasksCsvAsync(long projectId, long currentUserId)
    {
        var access = await _dbContext.ResolveAccessAsync(projectId, currentUserId);
        if (access is null || !access.Value.CanView)
        {
            return null;
        }

        var workspaceId = await _dbContext.Projects
            .Where(p => p.Id == projectId).Select(p => p.WorkspaceId).FirstAsync();
        var statusNameByKey = await _dbContext.WorkflowStates
            .Where(s => s.WorkspaceId == workspaceId)
            .ToDictionaryAsync(s => s.Key, s => s.Name);

        var tasks = await _dbContext.Tasks
            .Where(t => t.ProjectId == projectId)
            .OrderBy(t => t.Id)
            .Select(t => new
            {
                t.Title,
                t.Status,
                AssigneeName = t.Assignee != null ? t.Assignee.Name : null,
                t.Priority,
                t.DueDate,
                t.EstimatePoints,
                Labels = t.TaskLabels
                    .Where(tl => tl.Label != null)
                    .OrderBy(tl => tl.Label.Name)
                    .Select(tl => tl.Label.Name)
                    .ToList(),
            })
            .ToListAsync();

        var sb = new StringBuilder();
        sb.Append(CsvWriter.Row("タイトル", "状態", "担当者", "優先度", "期限", "見積", "ラベル"));
        foreach (var t in tasks)
        {
            sb.Append(CsvWriter.Row(
                t.Title,
                statusNameByKey.GetValueOrDefault(t.Status, t.Status),
                t.AssigneeName ?? "",
                PriorityLabels.GetValueOrDefault(t.Priority, t.Priority),
                t.DueDate?.ToString("yyyy-MM-dd") ?? "",
                t.EstimatePoints?.ToString() ?? "",
                string.Join(";", t.Labels)));
        }

        return CsvWriter.ToBomBytes(sb.ToString());
    }
}
