using System.Net;
using System.Net.Mail;

namespace TaskManagementSystem.Api.Services;

/// <summary>
/// SMTP によるメール送信(自己ホスト向け。M5 §8)。.NET 標準の <see cref="SmtpClient"/> を使い、
/// 追加依存なしで標準的な SMTP(25 / 587 STARTTLS + 認証)に対応する。
/// 本文は招待トークンを含むためログに出さない(宛先のみ記録)。
/// </summary>
public class SmtpEmailSender : IEmailSender
{
    private readonly SmtpOptions _options;
    private readonly ILogger<SmtpEmailSender> _logger;

    public SmtpEmailSender(SmtpOptions options, ILogger<SmtpEmailSender> logger)
    {
        _options = options;
        _logger = logger;
    }

    public async Task SendAsync(string toEmail, string subject, string body, CancellationToken cancellationToken = default)
    {
        using var message = BuildMessage(_options, toEmail, subject, body);
        using var client = new SmtpClient(_options.Host, _options.Port)
        {
            EnableSsl = _options.UseStartTls,
            DeliveryMethod = SmtpDeliveryMethod.Network,
        };

        if (!string.IsNullOrEmpty(_options.User))
        {
            client.Credentials = new NetworkCredential(_options.User, _options.Password);
        }

        try
        {
            await client.SendMailAsync(message, cancellationToken);
            // 本文(トークンを含む)は記録しない
            _logger.LogInformation("メールを送信しました 宛先={ToEmail} 件名={Subject}", toEmail, subject);
        }
        catch (Exception ex)
        {
            // 送信失敗はログに残す(例外は上位へ伝播させ、呼び出し側で扱う)
            _logger.LogError(ex, "メール送信に失敗しました 宛先={ToEmail} 件名={Subject}", toEmail, subject);
            throw;
        }
    }

    /// <summary>送信メッセージを組み立てる(送信元の解決・表示名の付与)。テスト容易化のため分離。</summary>
    internal static MailMessage BuildMessage(SmtpOptions options, string toEmail, string subject, string body)
    {
        var from = options.ResolveFrom();
        var message = new MailMessage
        {
            From = string.IsNullOrWhiteSpace(options.FromName)
                ? new MailAddress(from)
                : new MailAddress(from, options.FromName),
            Subject = subject,
            Body = body,
        };
        message.To.Add(toEmail);
        return message;
    }
}
