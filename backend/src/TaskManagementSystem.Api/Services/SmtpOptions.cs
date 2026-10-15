namespace TaskManagementSystem.Api.Services;

/// <summary>
/// SMTP 送信の設定(自己ホスト向け。M5 §8)。設定セクション <c>Smtp</c> から束縛する。
/// <c>Host</c> が設定されていれば実送信(<see cref="SmtpEmailSender"/>)、無ければログ出力に留める。
/// 標準的な SMTP(25 / 587 STARTTLS + 認証)に対応し、各プロバイダ(Gmail・SES・SendGrid 等)で使える。
/// </summary>
public class SmtpOptions
{
    public const string SectionName = "Smtp";

    /// <summary>SMTP サーバーのホスト名。未設定なら実送信しない。</summary>
    public string? Host { get; set; }

    /// <summary>ポート。既定 587(STARTTLS)。平文は 25。</summary>
    public int Port { get; set; } = 587;

    /// <summary>認証ユーザー名(任意。未設定なら匿名送信)。</summary>
    public string? User { get; set; }

    /// <summary>認証パスワード(任意)。ログには出さない。</summary>
    public string? Password { get; set; }

    /// <summary>送信元アドレス。未設定なら <see cref="User"/> を使う。</summary>
    public string? From { get; set; }

    /// <summary>送信元の表示名(任意)。</summary>
    public string? FromName { get; set; }

    /// <summary>STARTTLS を使うか。既定 true(587)。平文 25 のみで送る場合は false。</summary>
    public bool UseStartTls { get; set; } = true;

    /// <summary>実送信に必要な最低限(ホスト)が設定されているか。</summary>
    public bool IsConfigured => !string.IsNullOrWhiteSpace(Host);

    /// <summary>送信元アドレスを解決する(From 優先、無ければ User)。</summary>
    public string ResolveFrom() => !string.IsNullOrWhiteSpace(From) ? From! : (User ?? string.Empty);
}
