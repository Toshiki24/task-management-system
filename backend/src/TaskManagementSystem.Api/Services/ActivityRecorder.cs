using System.Text.Json;
using TaskManagementSystem.Api.Data;
using TaskManagementSystem.Api.Models;

namespace TaskManagementSystem.Api.Services;

/// <summary>
/// アクティビティ(活動)を記録するための小さなヘルパー(Phase 2 M3 §5)。
/// 呼び出し側の SaveChanges に相乗りさせる(ここでは Add のみ。保存は呼び出し側)。
/// </summary>
internal static class ActivityRecorder
{
    public static void Record(
        AppDbContext dbContext, long projectId, long? taskId, long actorUserId, string verb, object? payload = null)
    {
        dbContext.Activities.Add(new Activity
        {
            ProjectId = projectId,
            TaskId = taskId,
            ActorUserId = actorUserId,
            Verb = verb,
            Payload = payload is null ? null : JsonSerializer.Serialize(payload),
        });
    }
}
