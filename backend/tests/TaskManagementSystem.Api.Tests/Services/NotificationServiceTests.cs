using TaskManagementSystem.Api.Dtos.Comments;
using TaskManagementSystem.Api.Dtos.Notifications;
using TaskManagementSystem.Api.Dtos.Tasks;
using TaskManagementSystem.Api.Models;
using TaskManagementSystem.Api.Services;
using TaskManagementSystem.Api.Tests.Infrastructure;

namespace TaskManagementSystem.Api.Tests.Services;

public class NotificationServiceTests : IClassFixture<TestDatabaseFixture>
{
    private readonly TestDatabaseFixture _db;

    public NotificationServiceTests(TestDatabaseFixture db)
    {
        _db = db;
    }

    private static TaskRequest Req(string title, long? assigneeId = null, string? status = null) =>
        new(assigneeId, title, null, status, null, null, null, null, null);

    private static NotificationQuery Query(bool unreadOnly = false) =>
        new() { UnreadOnly = unreadOnly };

    [Fact(DisplayName = "M3 担当にされると ASSIGNED 通知が届く。自分への割り当ては通知しない")]
    public async Task Assigned_NotifiesAssigneeNotSelf()
    {
        await using var ctx = _db.CreateContext();
        var (project, owner) = await TestData.CreateProjectWithOwnerAsync(ctx);
        var member = await TestData.CreateUserAsync(ctx);
        await TestData.AddMemberAsync(ctx, project.Id, member.Id, ProjectMemberRole.Member);
        var tasks = new TaskService(ctx);
        var notifications = new NotificationService(ctx);

        await tasks.CreateAsync(project.Id, Req("担当付き", member.Id), owner.Id);

        var memberList = await notifications.GetAsync(member.Id, Query());
        Assert.Contains(memberList.Items, n => n.Type == NotificationType.Assigned && n.ActorUserId == owner.Id);
        // 実行者(owner)自身には通知されない
        Assert.Empty((await notifications.GetAsync(owner.Id, Query())).Items);
    }

    [Fact(DisplayName = "M3 状態変更でウォッチャー(実行者除く)に STATUS_CHANGED 通知")]
    public async Task StatusChanged_NotifiesWatchers()
    {
        await using var ctx = _db.CreateContext();
        var (project, owner) = await TestData.CreateProjectWithOwnerAsync(ctx);
        var member = await TestData.CreateUserAsync(ctx);
        await TestData.AddMemberAsync(ctx, project.Id, member.Id, ProjectMemberRole.Member);
        var tasks = new TaskService(ctx);
        var notifications = new NotificationService(ctx);
        // member を担当=自動ウォッチにして、owner が状態を変更
        var task = (await tasks.CreateAsync(project.Id, Req("状態変更", member.Id, "TODO"), owner.Id)).Data!;

        await tasks.MoveAsync(task.Id, new MoveTaskRequest("DONE", null), owner.Id);

        var memberList = await notifications.GetAsync(member.Id, Query());
        var changed = memberList.Items.First(n => n.Type == NotificationType.StatusChanged);
        Assert.Equal("TODO", changed.Payload!.Value.GetProperty("from").GetString());
        Assert.Equal("DONE", changed.Payload!.Value.GetProperty("to").GetString());
    }

    [Fact(DisplayName = "M3 コメントで被メンションは MENTION、他ウォッチャーは COMMENT。投稿者は通知なし")]
    public async Task Comment_MentionAndWatcherNotifications()
    {
        await using var ctx = _db.CreateContext();
        var (project, owner) = await TestData.CreateProjectWithOwnerAsync(ctx);
        var assignee = await TestData.CreateUserAsync(ctx);
        var mentioned = await TestData.CreateUserAsync(ctx);
        await TestData.AddMemberAsync(ctx, project.Id, assignee.Id, ProjectMemberRole.Member);
        await TestData.AddMemberAsync(ctx, project.Id, mentioned.Id, ProjectMemberRole.Member);
        var tasks = new TaskService(ctx);
        var comments = new CommentService(ctx);
        var notifications = new NotificationService(ctx);
        // assignee を担当=ウォッチャーに
        var task = (await tasks.CreateAsync(project.Id, Req("コメント", assignee.Id), owner.Id)).Data!;

        // owner が mentioned を @ して投稿
        await comments.CreateAsync(task.Id, owner.Id, new CommentRequest($"@{mentioned.Name} 確認を"));

        // 被メンションは MENTION
        Assert.Contains((await notifications.GetAsync(mentioned.Id, Query())).Items, n => n.Type == NotificationType.Mention);
        // assignee(ウォッチャー・非メンション)は COMMENT
        Assert.Contains((await notifications.GetAsync(assignee.Id, Query())).Items, n => n.Type == NotificationType.Comment);
        // 投稿者 owner には通知されない
        Assert.Empty((await notifications.GetAsync(owner.Id, Query())).Items);
    }

    [Fact(DisplayName = "M3 既読化・未読件数・未読のみ取得が機能する")]
    public async Task ReadStateAndUnreadCount()
    {
        await using var ctx = _db.CreateContext();
        var (project, owner) = await TestData.CreateProjectWithOwnerAsync(ctx);
        var member = await TestData.CreateUserAsync(ctx);
        await TestData.AddMemberAsync(ctx, project.Id, member.Id, ProjectMemberRole.Member);
        var tasks = new TaskService(ctx);
        var notifications = new NotificationService(ctx);
        var task = (await tasks.CreateAsync(project.Id, Req("通知1", member.Id), owner.Id)).Data!;
        await tasks.MoveAsync(task.Id, new MoveTaskRequest("DONE", null), owner.Id); // member へ STATUS_CHANGED

        Assert.Equal(2, await notifications.GetUnreadCountAsync(member.Id));

        var first = (await notifications.GetAsync(member.Id, Query())).Items.First();
        Assert.True(await notifications.MarkReadAsync(first.Id, member.Id));
        Assert.Equal(1, await notifications.GetUnreadCountAsync(member.Id));
        Assert.Single((await notifications.GetAsync(member.Id, Query(unreadOnly: true))).Items);

        Assert.Equal(1, await notifications.MarkAllReadAsync(member.Id));
        Assert.Equal(0, await notifications.GetUnreadCountAsync(member.Id));
    }

    [Fact(DisplayName = "M3 他人の通知は既読化できない")]
    public async Task MarkRead_OnlyOwn()
    {
        await using var ctx = _db.CreateContext();
        var (project, owner) = await TestData.CreateProjectWithOwnerAsync(ctx);
        var member = await TestData.CreateUserAsync(ctx);
        await TestData.AddMemberAsync(ctx, project.Id, member.Id, ProjectMemberRole.Member);
        var tasks = new TaskService(ctx);
        var notifications = new NotificationService(ctx);
        await tasks.CreateAsync(project.Id, Req("担当", member.Id), owner.Id);
        var memberNotif = (await notifications.GetAsync(member.Id, Query())).Items.First();

        // owner は member 宛の通知を既読にできない
        Assert.False(await notifications.MarkReadAsync(memberNotif.Id, owner.Id));
    }
}
