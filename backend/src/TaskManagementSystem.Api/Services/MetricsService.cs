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

    public async Task<DevMetricsDto?> GetProjectDevMetricsAsync(long projectId, long currentUserId, int days)
    {
        var access = await _dbContext.ResolveAccessAsync(projectId, currentUserId);
        if (access is null || !access.Value.CanView)
        {
            return null;
        }

        days = Math.Clamp(days, 1, 90);
        var workspaceId = await _dbContext.Projects
            .Where(p => p.Id == projectId).Select(p => p.WorkspaceId).FirstAsync();

        // 状態キー→カテゴリ
        var categoryByKey = await _dbContext.WorkflowStates
            .Where(s => s.WorkspaceId == workspaceId)
            .ToDictionaryAsync(s => s.Key, s => s.Category);

        // プロジェクトのタスク作成時刻
        var tasks = await _dbContext.Tasks
            .Where(t => t.ProjectId == projectId)
            .Select(t => new { t.Id, t.CreatedAt })
            .ToListAsync();
        var createdById = tasks.ToDictionary(t => t.Id, t => t.CreatedAt);

        // 状態遷移の履歴(古い順)。IN_PROGRESS/DONE に初めて入った時刻を取り出す
        var histories = await _dbContext.TaskStatusHistories
            .Where(h => h.Task.ProjectId == projectId)
            .OrderBy(h => h.CreatedAt).ThenBy(h => h.Id)
            .Select(h => new { h.TaskId, h.ToStatus, h.CreatedAt })
            .ToListAsync();

        var firstInProgress = new Dictionary<long, DateTime>();
        var firstDone = new Dictionary<long, DateTime>();
        foreach (var h in histories)
        {
            if (!categoryByKey.TryGetValue(h.ToStatus, out var category))
            {
                continue;
            }

            if (category == WorkflowStateCategory.InProgress && !firstInProgress.ContainsKey(h.TaskId))
            {
                firstInProgress[h.TaskId] = h.CreatedAt;
            }
            else if (category == WorkflowStateCategory.Done && !firstDone.ContainsKey(h.TaskId))
            {
                firstDone[h.TaskId] = h.CreatedAt;
            }
        }

        var today = DateOnly.FromDateTime(JstNow());
        var from = today.AddDays(-(days - 1));

        var cycleSamples = new List<double>();
        var leadSamples = new List<double>();
        var completedByDay = new Dictionary<DateOnly, int>();

        foreach (var (taskId, doneAt) in firstDone)
        {
            var doneDate = JstDate(doneAt);
            if (doneDate < from || doneDate > today)
            {
                continue; // 期間内に完了したタスクのみを対象にする
            }

            completedByDay[doneDate] = completedByDay.GetValueOrDefault(doneDate, 0) + 1;

            if (firstInProgress.TryGetValue(taskId, out var inProgressAt) && doneAt >= inProgressAt)
            {
                cycleSamples.Add((doneAt - inProgressAt).TotalHours);
            }

            if (createdById.TryGetValue(taskId, out var createdAt) && doneAt >= createdAt)
            {
                leadSamples.Add((doneAt - createdAt).TotalHours);
            }
        }

        // 期間の全日を 0 埋めで並べる(グラフが連続するように)
        var throughput = Enumerable.Range(0, days)
            .Select(offset => from.AddDays(offset))
            .Select(date => new ThroughputPointDto(date, completedByDay.GetValueOrDefault(date, 0)))
            .ToList();

        return new DevMetricsDto(
            days,
            cycleSamples.Count == 0 ? null : Math.Round(cycleSamples.Average(), 1),
            leadSamples.Count == 0 ? null : Math.Round(leadSamples.Average(), 1),
            completedByDay.Values.Sum(),
            throughput);
    }

    /// <summary>保存時刻(UTC 相当)を JST の日付に変換する。</summary>
    private static DateOnly JstDate(DateTime utc) =>
        DateOnly.FromDateTime(DateTime.SpecifyKind(utc, DateTimeKind.Utc).AddHours(9));

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
