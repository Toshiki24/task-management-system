namespace TaskManagementSystem.Api.Services;

/// <summary>
/// メール送信のプレースホルダ実装。実際には送信せず、送信内容をログに記録する。
/// M1 ではメール基盤(SES/SMTP)を接続しないため、招待リンクはログで確認する運用とする。
/// 本文(招待リンク=トークンを含む)はログに出るため、本番でのログ取り扱いには注意する。
/// </summary>
public class LoggingEmailSender : IEmailSender
{
    private readonly ILogger<LoggingEmailSender> _logger;

    public LoggingEmailSender(ILogger<LoggingEmailSender> logger)
    {
        _logger = logger;
    }

    public Task SendAsync(string toEmail, string subject, string body, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("メール送信(プレースホルダ) 宛先={ToEmail} 件名={Subject}\n{Body}",
            toEmail, subject, body);
        return Task.CompletedTask;
    }
}
