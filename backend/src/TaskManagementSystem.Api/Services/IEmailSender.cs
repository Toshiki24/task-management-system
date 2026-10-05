namespace TaskManagementSystem.Api.Services;

/// <summary>
/// メール送信の抽象化(Phase 2 M1 §4)。M1 では招待メールのみに使う最小実装。
/// AWS では SES、自己ホストでは SMTP の実装に差し替えられるようにする(M3 の通知で拡張予定)。
/// </summary>
public interface IEmailSender
{
    Task SendAsync(string toEmail, string subject, string body, CancellationToken cancellationToken = default);
}
