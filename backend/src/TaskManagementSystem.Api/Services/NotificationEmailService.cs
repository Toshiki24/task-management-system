using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TaskManagementSystem.Api.Data;
using TaskManagementSystem.Api.Models;

namespace TaskManagementSystem.Api.Services;

/// <summary>
/// アプリ内通知に対応するメールを送信する(Phase 2 M5 §8)。
/// SMTP 未設定(LoggingEmailSender)の環境ではログ出力のみで実害はない。
/// </summary>
public class NotificationEmailService : INotificationEmailService
{
    private readonly AppDbContext _dbContext;
    private readonly IEmailSender _emailSender;
    private readonly ILogger<NotificationEmailService> _logger;

    public NotificationEmailService(
        AppDbContext dbContext, IEmailSender emailSender, ILogger<NotificationEmailService> logger)
    {
        _dbContext = dbContext;
        _emailSender = emailSender;
        _logger = logger;
    }

    public async Task SendAsync(
        IReadOnlyCollection<Notification> notifications, CancellationToken cancellationToken = default)
    {
        if (notifications.Count == 0)
        {
            return;
        }

        // 受信者のうちメール通知が有効な人だけを対象にする(無効ならアプリ内通知のみ残す)
        var recipientIds = notifications.Select(n => n.RecipientUserId).Distinct().ToList();
        var recipients = await _dbContext.Users
            .Where(u => recipientIds.Contains(u.Id) && u.EmailNotificationsEnabled)
            .Select(u => new { u.Id, u.Email })
            .ToDictionaryAsync(u => u.Id, u => u.Email, cancellationToken);
        if (recipients.Count == 0)
        {
            return;
        }

        // 本文に使うタスク名・実行者名をまとめて引く
        var taskIds = notifications.Where(n => n.TaskId.HasValue).Select(n => n.TaskId!.Value).Distinct().ToList();
        var taskTitles = taskIds.Count == 0
            ? new Dictionary<long, string>()
            : await _dbContext.Tasks
                .Where(t => taskIds.Contains(t.Id))
                .Select(t => new { t.Id, t.Title })
                .ToDictionaryAsync(t => t.Id, t => t.Title, cancellationToken);

        var actorIds = notifications.Where(n => n.ActorUserId.HasValue)
            .Select(n => n.ActorUserId!.Value).Distinct().ToList();
        var actorNames = actorIds.Count == 0
            ? new Dictionary<long, string>()
            : await _dbContext.Users
                .Where(u => actorIds.Contains(u.Id))
                .Select(u => new { u.Id, u.Name })
                .ToDictionaryAsync(u => u.Id, u => u.Name, cancellationToken);

        foreach (var notification in notifications)
        {
            if (!recipients.TryGetValue(notification.RecipientUserId, out var toEmail))
            {
                continue;
            }

            var taskTitle = notification.TaskId is { } tid && taskTitles.TryGetValue(tid, out var title)
                ? title
                : "(不明なタスク)";
            var actorName = notification.ActorUserId is { } aid && actorNames.TryGetValue(aid, out var name)
                ? name
                : null;

            var (subject, body) = BuildMessage(notification, taskTitle, actorName);

            try
            {
                await _emailSender.SendAsync(toEmail, subject, body, cancellationToken);
            }
            catch (Exception ex)
            {
                // 送信失敗は握りつぶす(アプリ内通知は保存済みのため情報は失われない)
                _logger.LogWarning(ex,
                    "通知メールの送信に失敗しました 宛先={ToEmail} 種別={Type}", toEmail, notification.Type);
            }
        }
    }

    private static (string Subject, string Body) BuildMessage(
        Notification notification, string taskTitle, string? actorName)
    {
        var actor = string.IsNullOrEmpty(actorName) ? "ユーザー" : actorName;
        return notification.Type switch
        {
            NotificationType.Mention => (
                $"[タスク管理] {actor}さんがコメントであなたをメンションしました",
                $"{actor}さんがタスク「{taskTitle}」のコメントであなたをメンションしました。"),
            NotificationType.Comment => (
                $"[タスク管理] 「{taskTitle}」に新しいコメント",
                $"{actor}さんがタスク「{taskTitle}」にコメントしました。"),
            NotificationType.Assigned => (
                $"[タスク管理] タスクの担当者に割り当てられました",
                $"{actor}さんがあなたをタスク「{taskTitle}」の担当者に設定しました。"),
            NotificationType.StatusChanged => (
                $"[タスク管理] 「{taskTitle}」の状態が変更されました",
                BuildStatusChangedBody(notification, taskTitle, actor)),
            NotificationType.DueSoon => (
                $"[タスク管理] 「{taskTitle}」の期限が近づいています",
                $"タスク「{taskTitle}」の期限が近づいています。進捗を確認してください。"),
            NotificationType.DueOverdue => (
                $"[タスク管理] 「{taskTitle}」の期限が過ぎています",
                $"タスク「{taskTitle}」の期限が過ぎています。対応をお願いします。"),
            _ => (
                $"[タスク管理] 「{taskTitle}」に関する通知",
                $"タスク「{taskTitle}」に関する通知があります。"),
        };
    }

    private static string BuildStatusChangedBody(Notification notification, string taskTitle, string actor)
    {
        var (from, to) = ParseStatusChange(notification.Payload);
        var change = from is not null && to is not null ? $"({from} → {to})" : string.Empty;
        return $"{actor}さんがタスク「{taskTitle}」の状態を変更しました{change}。";
    }

    private static (string? From, string? To) ParseStatusChange(string? payload)
    {
        if (string.IsNullOrEmpty(payload))
        {
            return (null, null);
        }

        try
        {
            using var doc = JsonDocument.Parse(payload);
            var root = doc.RootElement;
            var from = root.TryGetProperty("from", out var f) ? f.GetString() : null;
            var to = root.TryGetProperty("to", out var t) ? t.GetString() : null;
            return (from, to);
        }
        catch (JsonException)
        {
            return (null, null);
        }
    }
}
