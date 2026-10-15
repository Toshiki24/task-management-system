using Microsoft.Extensions.Configuration;
using TaskManagementSystem.Api.Services;

namespace TaskManagementSystem.Api.Tests.Services;

public class SmtpEmailSenderTests
{
    [Fact(DisplayName = "Smtp:Host が無ければ未設定、あれば設定済みと判定する")]
    public void SmtpOptions_IsConfigured()
    {
        Assert.False(new SmtpOptions().IsConfigured);
        Assert.False(new SmtpOptions { Host = "  " }.IsConfigured);
        Assert.True(new SmtpOptions { Host = "smtp.example.com" }.IsConfigured);
    }

    [Fact(DisplayName = "送信元は From 優先、無ければ User を使う")]
    public void SmtpOptions_ResolveFrom()
    {
        Assert.Equal("noreply@example.com", new SmtpOptions { From = "noreply@example.com", User = "u@example.com" }.ResolveFrom());
        Assert.Equal("u@example.com", new SmtpOptions { User = "u@example.com" }.ResolveFrom());
        Assert.Equal(string.Empty, new SmtpOptions().ResolveFrom());
    }

    [Fact(DisplayName = "設定セクションから束縛できる(環境変数相当のキー)")]
    public void SmtpOptions_BindsFromConfiguration()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Smtp:Host"] = "smtp.example.com",
                ["Smtp:Port"] = "465",
                ["Smtp:User"] = "user@example.com",
                ["Smtp:Password"] = "secret",
                ["Smtp:From"] = "noreply@example.com",
                ["Smtp:FromName"] = "タスク管理",
                ["Smtp:UseStartTls"] = "false",
            })
            .Build();

        var options = config.GetSection(SmtpOptions.SectionName).Get<SmtpOptions>()!;

        Assert.Equal("smtp.example.com", options.Host);
        Assert.Equal(465, options.Port);
        Assert.Equal("user@example.com", options.User);
        Assert.Equal("noreply@example.com", options.ResolveFrom());
        Assert.Equal("タスク管理", options.FromName);
        Assert.False(options.UseStartTls);
        Assert.True(options.IsConfigured);
    }

    [Fact(DisplayName = "送信メッセージは宛先・件名・本文・送信元(表示名)を設定する")]
    public void BuildMessage_SetsFields()
    {
        var options = new SmtpOptions { From = "noreply@example.com", FromName = "タスク管理" };

        using var message = SmtpEmailSender.BuildMessage(options, "to@example.test", "件名", "本文\nリンク");

        Assert.Equal("noreply@example.com", message.From!.Address);
        Assert.Equal("タスク管理", message.From.DisplayName);
        Assert.Single(message.To);
        Assert.Equal("to@example.test", message.To[0].Address);
        Assert.Equal("件名", message.Subject);
        Assert.Equal("本文\nリンク", message.Body);
    }

    [Fact(DisplayName = "表示名が無ければアドレスのみの送信元になる")]
    public void BuildMessage_WithoutFromName()
    {
        var options = new SmtpOptions { User = "u@example.com" };

        using var message = SmtpEmailSender.BuildMessage(options, "to@example.test", "s", "b");

        Assert.Equal("u@example.com", message.From!.Address);
        Assert.Equal(string.Empty, message.From.DisplayName);
    }
}
