using System.Text.Json;

namespace TaskManagementSystem.Api.Dtos.Notifications;

/// <summary>アプリ内通知 1 件(M3 §6)。対象タスク・実行者は削除済みだと null。</summary>
public record NotificationDto(
    long Id,
    string Type,
    long? TaskId,
    string? TaskTitle,
    long? ActorUserId,
    string? ActorName,
    JsonElement? Payload,
    bool IsRead,
    DateTime CreatedAt);

/// <summary>通知一覧の取得条件(ページング＋未読のみ)。</summary>
public class NotificationQuery
{
    public int? Page { get; set; }
    public int? PageSize { get; set; }
    public bool? UnreadOnly { get; set; }
}

/// <summary>未読件数。</summary>
public record UnreadCountDto(int Count);
