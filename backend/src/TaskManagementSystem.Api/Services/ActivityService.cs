using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TaskManagementSystem.Api.Data;
using TaskManagementSystem.Api.Dtos.Tasks;

namespace TaskManagementSystem.Api.Services;

/// <summary>
/// アクティビティの参照(Phase 2 M3 §5)。記録は各サービス(TaskService/CommentService)が
/// ActivityRecorder 経由で行い、ここは読み取り専用。可視性は M1 の ResolveTaskAccess に集約。すべて LINQ/EF。
/// </summary>
public class ActivityService : IActivityService
{
    private readonly AppDbContext _dbContext;

    public ActivityService(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<List<ActivityDto>?> GetByTaskAsync(long taskId, long currentUserId)
    {
        var access = await _dbContext.ResolveTaskAccessAsync(taskId, currentUserId);
        if (access is null || !access.Value.CanView)
        {
            return null;
        }

        // payload(jsonb 文字列)はクエリ後にパースするため、いったん素の形で取得する
        var rows = await _dbContext.Activities
            .Where(a => a.TaskId == taskId)
            .OrderByDescending(a => a.CreatedAt)
            .ThenByDescending(a => a.Id)
            .Select(a => new
            {
                a.Id,
                a.TaskId,
                a.ActorUserId,
                ActorName = a.ActorUser.Name,
                a.Verb,
                a.Payload,
                a.CreatedAt,
            })
            .ToListAsync();

        return rows
            .Select(r => new ActivityDto(
                r.Id, r.TaskId, r.ActorUserId, r.ActorName, r.Verb, ParsePayload(r.Payload), r.CreatedAt))
            .ToList();
    }

    private static JsonElement? ParsePayload(string? payload)
    {
        if (string.IsNullOrWhiteSpace(payload))
        {
            return null;
        }

        try
        {
            return JsonDocument.Parse(payload).RootElement.Clone();
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
