using Microsoft.Extensions.Logging.Abstractions;
using TaskManagementSystem.Api.Data;
using TaskManagementSystem.Api.Models;
using TaskManagementSystem.Api.Services;
using TaskManagementSystem.Api.Tests.Infrastructure;

namespace TaskManagementSystem.Api.Tests.Services;

public class NotificationEmailServiceTests : IClassFixture<TestDatabaseFixture>
{
    private readonly TestDatabaseFixture _db;

    public NotificationEmailServiceTests(TestDatabaseFixture db)
    {
        _db = db;
    }

    private sealed record SentMail(string To, string Subject, string Body);

    private sealed class CapturingEmailSender : IEmailSender
    {
        public List<SentMail> Sent { get; } = new();

        public Task SendAsync(string toEmail, string subject, string body, CancellationToken cancellationToken = default)
        {
            Sent.Add(new SentMail(toEmail, subject, body));
            return Task.CompletedTask;
        }
    }

    private sealed class ThrowingEmailSender : IEmailSender
    {
        public Task SendAsync(string toEmail, string subject, string body, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("SMTP 失敗");
    }

    private static NotificationEmailService NewService(AppDbContext ctx, IEmailSender sender) =>
        new(ctx, sender, NullLogger<NotificationEmailService>.Instance);

    [Fact(DisplayName = "メール通知が有効な受信者にだけ送信し、無効な受信者には送らない")]
    public async Task SendAsync_OnlyEmailsEnabledRecipients()
    {
        await using var ctx = _db.CreateContext();
        var actor = await TestData.CreateUserAsync(ctx);
        var (project, _) = await TestData.CreateProjectWithOwnerAsync(ctx);
        var task = await TestData.CreateTaskAsync(ctx, project.Id);

        var enabled = await TestData.CreateUserAsync(ctx);
        var disabled = await TestData.CreateUserAsync(ctx);
        disabled.EmailNotificationsEnabled = false;
        await ctx.SaveChangesAsync();

        var notifications = NotificationRecorder.AddMany(
            ctx, new[] { enabled.Id, disabled.Id }, NotificationType.Mention, task.Id, actor.Id);
        await ctx.SaveChangesAsync();

        var sender = new CapturingEmailSender();
        await NewService(ctx, sender).SendAsync(notifications);

        var mail = Assert.Single(sender.Sent);
        Assert.Equal(enabled.Email, mail.To);
        Assert.Contains("メンション", mail.Subject);
    }

    [Fact(DisplayName = "種別ごとに件名・本文を組み立てる(タスク名・実行者名を含む)")]
    public async Task SendAsync_BuildsMessagePerType()
    {
        await using var ctx = _db.CreateContext();
        var actor = await TestData.CreateUserAsync(ctx);
        var (project, _) = await TestData.CreateProjectWithOwnerAsync(ctx);
        var task = await TestData.CreateTaskAsync(ctx, project.Id);
        var recipient = await TestData.CreateUserAsync(ctx);

        var notifications = new List<Notification>
        {
            NotificationRecorder.Add(ctx, recipient.Id, NotificationType.Assigned, task.Id, actor.Id),
            NotificationRecorder.Add(ctx, recipient.Id, NotificationType.StatusChanged, task.Id, actor.Id,
                new { from = "TODO", to = "DONE" }),
            NotificationRecorder.Add(ctx, recipient.Id, NotificationType.DueOverdue, task.Id, null),
        };
        await ctx.SaveChangesAsync();

        var sender = new CapturingEmailSender();
        await NewService(ctx, sender).SendAsync(notifications);

        Assert.Equal(3, sender.Sent.Count);
        Assert.All(sender.Sent, m => Assert.Equal(recipient.Email, m.To));
        Assert.Contains(sender.Sent, m => m.Subject.Contains("担当者") && m.Body.Contains(actor.Name));
        Assert.Contains(sender.Sent, m => m.Body.Contains("TODO → DONE"));
        Assert.Contains(sender.Sent, m => m.Subject.Contains("期限が過ぎています"));
    }

    [Fact(DisplayName = "送信失敗は握りつぶす(例外を投げない)")]
    public async Task SendAsync_SwallowsSenderFailure()
    {
        await using var ctx = _db.CreateContext();
        var actor = await TestData.CreateUserAsync(ctx);
        var (project, _) = await TestData.CreateProjectWithOwnerAsync(ctx);
        var task = await TestData.CreateTaskAsync(ctx, project.Id);
        var recipient = await TestData.CreateUserAsync(ctx);

        var notifications = new List<Notification>
        {
            NotificationRecorder.Add(ctx, recipient.Id, NotificationType.Comment, task.Id, actor.Id),
        };
        await ctx.SaveChangesAsync();

        // 例外が外に漏れないこと(アプリ内通知は保存済み)
        await NewService(ctx, new ThrowingEmailSender()).SendAsync(notifications);
    }

    [Fact(DisplayName = "通知が空なら何も送らない")]
    public async Task SendAsync_NoopForEmpty()
    {
        await using var ctx = _db.CreateContext();
        var sender = new CapturingEmailSender();
        await NewService(ctx, sender).SendAsync(Array.Empty<Notification>());
        Assert.Empty(sender.Sent);
    }
}
