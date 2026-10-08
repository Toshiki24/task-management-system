using System.Text.Json;
using TaskManagementSystem.Api.Data;
using TaskManagementSystem.Api.Models;

namespace TaskManagementSystem.Api.Services;

/// <summary>
/// アプリ内通知を生成するヘルパー(M3 §6)。保存は呼び出し側の SaveChanges に委ねる(Add のみ)。
/// 自分自身の操作では自分に通知しない運用のため、呼び出し側で actor を除外してから渡す。
/// </summary>
internal static class NotificationRecorder
{
    public static void Add(
        AppDbContext dbContext, long recipientUserId, string type, long? taskId, long? actorUserId,
        object? payload = null)
    {
        dbContext.Notifications.Add(new Notification
        {
            RecipientUserId = recipientUserId,
            Type = type,
            TaskId = taskId,
            ActorUserId = actorUserId,
            Payload = payload is null ? null : JsonSerializer.Serialize(payload),
        });
    }

    /// <summary>複数の受信者へ同じ通知を生成する(重複 ID は 1 回だけ)。</summary>
    public static void AddMany(
        AppDbContext dbContext, IEnumerable<long> recipientUserIds, string type, long? taskId, long? actorUserId,
        object? payload = null)
    {
        foreach (var recipientId in recipientUserIds.Distinct())
        {
            Add(dbContext, recipientId, type, taskId, actorUserId, payload);
        }
    }
}
