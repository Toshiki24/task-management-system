using TaskManagementSystem.Api.Models;

namespace TaskManagementSystem.Api.Services;

/// <summary>
/// 生成済みのアプリ内通知について、受信者へメールを送る(M3 §6)。
/// 送信は IEmailSender(本番は SES)に委譲し、1 通ずつ失敗を握りつぶす(リクエストを失敗させない)。
/// </summary>
public interface INotificationEmailSender
{
    Task SendForAsync(IReadOnlyList<Notification> created, CancellationToken cancellationToken = default);
}
