using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using TaskManagementSystem.Api.Dtos.Tasks;
using TaskManagementSystem.Api.Models;
using TaskManagementSystem.Api.Services;
using TaskManagementSystem.Api.Tests.Infrastructure;

namespace TaskManagementSystem.Api.Tests.Services;

public class NotificationEmailSenderTests : IClassFixture<TestDatabaseFixture>
{
    private readonly TestDatabaseFixture _db;

    public NotificationEmailSenderTests(TestDatabaseFixture db)
    {
        _db = db;
    }

    private sealed class CapturingEmailSender : IEmailSender
    {
        public List<(string To, string Subject, string Body)> Sent { get; } = new();

        public Task SendAsync(string toEmail, string subject, string body, CancellationToken cancellationToken = default)
        {
            Sent.Add((toEmail, subject, body));
            return Task.CompletedTask;
        }
    }

    private sealed class ThrowingEmailSender : IEmailSender
    {
        public Task SendAsync(string toEmail, string subject, string body, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("SMTP down");
    }

    private static NotificationEmailSender CreateSender(
        TaskManagementSystem.Api.Data.AppDbContext ctx, IEmailSender email)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["App:BaseUrl"] = "https://example.test" })
            .Build();
        return new NotificationEmailSender(ctx, email, config, NullLogger<NotificationEmailSender>.Instance);
    }

    private static TaskRequest Req(string title, long? assigneeId = null) =>
        new(assigneeId, title, null, null, null, null, null, null, null);

    [Fact(DisplayName = "M3 担当通知の生成時に担当者へメールが送られる(リンク付き)")]
    public async Task Create_SendsAssignedEmail()
    {
        await using var ctx = _db.CreateContext();
        var (project, owner) = await TestData.CreateProjectWithOwnerAsync(ctx);
        var member = await TestData.CreateUserAsync(ctx);
        await TestData.AddMemberAsync(ctx, project.Id, member.Id, ProjectMemberRole.Member);
        var email = new CapturingEmailSender();
        var tasks = new TaskService(ctx, CreateSender(ctx, email));

        var created = (await tasks.CreateAsync(project.Id, Req("メール対象", member.Id), owner.Id)).Data!;

        var sent = Assert.Single(email.Sent);
        Assert.Equal(member.Email, sent.To);
        Assert.Contains("担当", sent.Subject);
        Assert.Contains($"https://example.test/tasks/{created.Id}", sent.Body);
    }

    [Fact(DisplayName = "M3 自分を担当にした場合はメールを送らない")]
    public async Task Create_SelfAssign_NoEmail()
    {
        await using var ctx = _db.CreateContext();
        var (project, owner) = await TestData.CreateProjectWithOwnerAsync(ctx);
        var email = new CapturingEmailSender();
        var tasks = new TaskService(ctx, CreateSender(ctx, email));

        await tasks.CreateAsync(project.Id, Req("自分担当", owner.Id), owner.Id);

        Assert.Empty(email.Sent);
    }

    [Fact(DisplayName = "M3 メール送信が失敗してもタスク作成は成功する(握りつぶす)")]
    public async Task Create_EmailFailure_DoesNotThrow()
    {
        await using var ctx = _db.CreateContext();
        var (project, owner) = await TestData.CreateProjectWithOwnerAsync(ctx);
        var member = await TestData.CreateUserAsync(ctx);
        await TestData.AddMemberAsync(ctx, project.Id, member.Id, ProjectMemberRole.Member);
        var tasks = new TaskService(ctx, CreateSender(ctx, new ThrowingEmailSender()));

        var outcome = await tasks.CreateAsync(project.Id, Req("失敗しても作成", member.Id), owner.Id);

        Assert.Equal(CreateTaskResult.Success, outcome.Result);
    }
}
