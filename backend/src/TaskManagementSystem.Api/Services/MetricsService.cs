using Microsoft.EntityFrameworkCore;
using TaskManagementSystem.Api.Data;
using TaskManagementSystem.Api.Dtos.Metrics;
using TaskManagementSystem.Api.Models;

namespace TaskManagementSystem.Api.Services;

/// <summary>
/// 開発の見える化(基本指標。M5 §2)。進捗(状態別件数・完了率)、担当別の負荷、期限超過を
/// 既存データ(tasks / workflow_states)から集計する。可視性は 3 スコープに従う。すべて LINQ/EF。
/// </summary>
public class MetricsService : IMetricsService
{
    private readonly AppDbContext _dbContext;

    public MetricsService(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<MetricsDto?> GetProjectMetricsAsync(long projectId, long currentUserId)
    {
        var access = await _dbContext.ResolveAccessAsync(projectId, currentUserId);
        if (access is null || !access.Value.CanView)
        {
            return null;
        }

        var workspaceId = await _dbContext.Projects
            .Where(p => p.Id == projectId).Select(p => p.WorkspaceId).FirstAsync();

        // WS のワークフロー(状態キー→表示名・カテゴリ・並び)
        var states = await _dbContext.WorkflowStates
            .Where(s => s.WorkspaceId == workspaceId)
            .OrderBy(s => s.Position).ThenBy(s => s.Id)
            .Select(s => new { s.Key, s.Name, s.Category })
            .ToListAsync();
        var categoryByKey = states.ToDictionary(s => s.Key, s => s.Category);

        // プロジェクトのタスク(集計に必要な最小列のみ取得し、件数が限られる単位でメモリ集計する)
        var tasks = await _dbContext.Tasks
            .Where(t => t.ProjectId == projectId)
            .Select(t => new
            {
                t.Status,
                t.AssigneeId,
                AssigneeName = t.Assignee != null ? t.Assignee.Name : null,
                t.EstimatePoints,
                t.DueDate,
            })
            .ToListAsync();

        var total = tasks.Count;
        var today = DateOnly.FromDateTime(JstNow());

        bool IsClosed(string status) =>
            categoryByKey.TryGetValue(status, out var cat) && WorkflowStateCategory.IsClosed(cat);
        bool IsDone(string status) =>
            categoryByKey.TryGetValue(status, out var cat) && cat == WorkflowStateCategory.Done;

        var doneCount = tasks.Count(t => IsDone(t.Status));
        var completionRate = total == 0 ? 0 : Math.Round((double)doneCount / total, 3);

        var overdueCount = tasks.Count(t =>
            t.DueDate is { } due && due < today && !IsClosed(t.Status));

        // 状態別件数(ワークフローの並び順。未知の status はワークフロー外なので集計対象外)
        var countByStatus = tasks.GroupBy(t => t.Status).ToDictionary(g => g.Key, g => g.Count());
        var statusCounts = states
            .Select(s => new StatusCountDto(s.Key, s.Name, s.Category, countByStatus.GetValueOrDefault(s.Key, 0)))
            .ToList();

        // 担当別の負荷(未完了のみ)。未割り当ては AssigneeId=null でまとめる
        var assigneeLoads = tasks
            .Where(t => !IsClosed(t.Status))
            .GroupBy(t => new { t.AssigneeId, t.AssigneeName })
            .Select(g => new AssigneeLoadDto(
                g.Key.AssigneeId,
                g.Key.AssigneeName ?? "未割り当て",
                g.Count(),
                g.Sum(t => t.EstimatePoints ?? 0)))
            .OrderByDescending(a => a.OpenCount)
            .ToList();

        return new MetricsDto(total, doneCount, completionRate, overdueCount, statusCounts, assigneeLoads);
    }

    private static DateTime JstNow()
    {
        try
        {
            var jst = TimeZoneInfo.FindSystemTimeZoneById("Asia/Tokyo");
            return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, jst);
        }
        catch (TimeZoneNotFoundException)
        {
            return DateTime.UtcNow.AddHours(9);
        }
    }
}
