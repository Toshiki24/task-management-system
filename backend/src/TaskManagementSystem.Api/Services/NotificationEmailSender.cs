using Microsoft.EntityFrameworkCore;
using TaskManagementSystem.Api.Data;
using TaskManagementSystem.Api.Models;

namespace TaskManagementSystem.Api.Services;

/// <summary>
/// 通知メールの本文組み立てと送信(M3 §6)。受信者/実行者/タスクをまとめて引き、1 通ずつ IEmailSender に渡す。
/// 送信失敗は記録するだけで再スローしない(通知の永続化は済んでおり、アプリ内通知は別途届くため)。
/// </summary>
public class NotificationEmailSender : INotificationEmailSender
{
    private readonly AppDbContext _dbContext;
    private readonly IEmailSender _emailSender;
    private readonly ILogger<NotificationEmailSender> _logger;
    private readonly string _taskUrlBase;

    public NotificationEmailSender(
        AppDbContext dbContext,
        IEmailSender emailSender,
        IConfiguration configuration,
        ILogger<NotificationEmailSender> logger)
    {
        _dbContext = dbContext;
        _emailSender = emailSender;
        _logger = logger;
        // 例: https://tms.accent24.jp 。未設定ならリンクは相対パスになる
        _taskUrlBase = (configuration.GetValue<string>("App:BaseUrl") ?? string.Empty).TrimEnd('/');
    }

    public async Task SendForAsync(IReadOnlyList<Notification> created, CancellationToken cancellationToken = default)
    {
        if (created.Count == 0)
        {
            return;
        }

        var userIds = created.Select(n => n.RecipientUserId)
            .Concat(created.Where(n => n.ActorUserId.HasValue).Select(n => n.ActorUserId!.Value))
            .Distinct()
            .ToList();
        var taskIds = created.Where(n => n.TaskId.HasValue).Select(n => n.TaskId!.Value).Distinct().ToList();

        var users = await _dbContext.Users
            .Where(u => userIds.Contains(u.Id))
            .Select(u => new { u.Id, u.Name, u.Email })
            .ToDictionaryAsync(u => u.Id, cancellationToken);
        var titles = await _dbContext.Tasks
            .Where(t => taskIds.Contains(t.Id))
            .Select(t => new { t.Id, t.Title })
            .ToDictionaryAsync(t => t.Id, t => t.Title, cancellationToken);

        foreach (var notification in created)
        {
            if (!users.TryGetValue(notification.RecipientUserId, out var recipient) ||
                string.IsNullOrWhiteSpace(recipient.Email))
            {
                continue;
            }

            var actorName = notification.ActorUserId is { } actorId && users.TryGetValue(actorId, out var actor)
                ? actor.Name
                : "システム";
            var taskTitle = notification.TaskId is { } taskId && titles.TryGetValue(taskId, out var title)
                ? title
                : "タスク";
            var (subject, line) = Compose(notification.Type, actorName, taskTitle);
            var url = notification.TaskId is { } tid ? $"{_taskUrlBase}/tasks/{tid}" : _taskUrlBase;
            var body = $"{line}\n\n{url}";

            try
            {
                await _emailSender.SendAsync(recipient.Email, subject, body, cancellationToken);
            }
            catch (Exception ex)
            {
                // メール送信の失敗はアプリの処理を止めない(アプリ内通知は別途保存済み)
                _logger.LogWarning(ex, "通知メールの送信に失敗しました。通知ID={NotificationId} 宛先={Email}",
                    notification.Id, recipient.Email);
            }
        }
    }

    private static (string Subject, string Line) Compose(string type, string actorName, string taskTitle) => type switch
    {
        NotificationType.Assigned =>
            ($"「{taskTitle}」の担当に設定されました", $"{actorName} があなたを「{taskTitle}」の担当に設定しました。"),
        NotificationType.Mention =>
            ($"「{taskTitle}」でメンションされました", $"{actorName} が「{taskTitle}」のコメントであなたをメンションしました。"),
        NotificationType.StatusChanged =>
            ($"「{taskTitle}」の状態が変更されました", $"{actorName} が「{taskTitle}」の状態を変更しました。"),
        NotificationType.Comment =>
            ($"「{taskTitle}」に新しいコメント", $"{actorName} が「{taskTitle}」にコメントしました。"),
        NotificationType.DueSoon =>
            ($"「{taskTitle}」の期限が近づいています", $"「{taskTitle}」の期限が近づいています。"),
        NotificationType.DueOverdue =>
            ($"「{taskTitle}」の期限が過ぎています", $"「{taskTitle}」の期限が過ぎています。"),
        _ => ($"「{taskTitle}」の通知", $"「{taskTitle}」に更新がありました。"),
    };
}
