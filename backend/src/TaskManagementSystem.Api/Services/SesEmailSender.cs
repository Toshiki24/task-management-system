using Amazon.SimpleEmailV2;
using Amazon.SimpleEmailV2.Model;

namespace TaskManagementSystem.Api.Services;

/// <summary>
/// Amazon SES(v2)によるメール送信(M3 §6)。本番でメールを実送信する実装。
/// 差出人アドレスは設定(Email:FromAddress)で与える。リージョンは実行環境(Lambda)の既定を使う。
/// 送信ドメインの検証・サンドボックス解除は運用側で実施する(README/設計のメモ参照)。
/// </summary>
public class SesEmailSender : IEmailSender
{
    private readonly IAmazonSimpleEmailServiceV2 _ses;
    private readonly string _fromAddress;

    public SesEmailSender(IAmazonSimpleEmailServiceV2 ses, IConfiguration configuration)
    {
        _ses = ses;
        _fromAddress = configuration.GetValue<string>("Email:FromAddress")
            ?? throw new InvalidOperationException("Email:FromAddress が設定されていません。");
    }

    public async Task SendAsync(
        string toEmail, string subject, string body, CancellationToken cancellationToken = default)
    {
        var request = new SendEmailRequest
        {
            FromEmailAddress = _fromAddress,
            Destination = new Destination { ToAddresses = new List<string> { toEmail } },
            Content = new EmailContent
            {
                Simple = new Message
                {
                    Subject = new Content { Data = subject, Charset = "UTF-8" },
                    Body = new Body { Text = new Content { Data = body, Charset = "UTF-8" } },
                },
            },
        };

        await _ses.SendEmailAsync(request, cancellationToken);
    }
}
