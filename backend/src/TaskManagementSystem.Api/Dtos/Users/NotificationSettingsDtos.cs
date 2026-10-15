namespace TaskManagementSystem.Api.Dtos.Users;

/// <summary>本人のメール通知設定(Phase 2 M5 §8)。</summary>
public record NotificationSettingsDto(bool EmailEnabled);

/// <summary>メール通知設定の更新リクエスト。</summary>
public record UpdateNotificationSettingsRequest(bool EmailEnabled);
