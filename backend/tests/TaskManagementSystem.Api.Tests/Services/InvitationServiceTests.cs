using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using TaskManagementSystem.Api.Data;
using TaskManagementSystem.Api.Dtos.Invitations;
using TaskManagementSystem.Api.Models;
using TaskManagementSystem.Api.Services;
using TaskManagementSystem.Api.Tests.Infrastructure;

namespace TaskManagementSystem.Api.Tests.Services;

public class InvitationServiceTests : IClassFixture<TestDatabaseFixture>
{
    private readonly TestDatabaseFixture _db;

    public InvitationServiceTests(TestDatabaseFixture db)
    {
        _db = db;
    }

    private sealed class CapturingEmailSender : IEmailSender
    {
        public string? To { get; private set; }
        public string? Body { get; private set; }

        public Task SendAsync(string toEmail, string subject, string body, CancellationToken cancellationToken = default)
        {
            To = toEmail;
            Body = body;
            return Task.CompletedTask;
        }
    }

    private static InvitationService NewService(AppDbContext ctx, IEmailSender email) =>
        new(ctx, TimeProvider.System, email, new ConfigurationBuilder().Build());

    private static string ExtractToken(string body)
    {
        const string marker = "token=";
        var idx = body.LastIndexOf(marker, StringComparison.Ordinal);
        return body[(idx + marker.Length)..].Trim();
    }

    private static string Hash(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token))).ToLowerInvariant();

    private async Task<(long WorkspaceId, User Admin)> CreateWorkspaceWithAdminAsync(AppDbContext ctx)
    {
        var admin = await TestData.CreateUserAsync(ctx);
        var workspace = await TestData.CreateWorkspaceAsync(ctx);
        await TestData.AddWorkspaceMemberAsync(ctx, workspace.Id, admin.Id, WorkspaceMemberRole.Admin);
        return (workspace.Id, admin);
    }

    [Fact(DisplayName = "招待発行は WS Admin のみ。Member は 403")]
    public async Task CreateAsync_ForbiddenForNonAdmin()
    {
        await using var ctx = _db.CreateContext();
        var (workspaceId, _) = await CreateWorkspaceWithAdminAsync(ctx);
        var member = await TestData.CreateUserAsync(ctx);
        await TestData.AddWorkspaceMemberAsync(ctx, workspaceId, member.Id, WorkspaceMemberRole.Member);

        var outcome = await NewService(ctx, new CapturingEmailSender()).CreateAsync(
            workspaceId, new CreateInvitationRequest("new@example.test", WorkspaceMemberRole.Member), member.Id);

        Assert.Equal(CreateInvitationResult.Forbidden, outcome.Result);
    }

    [Fact(DisplayName = "非所属ワークスペースへの招待は WorkspaceNotFound(存在を開示しない)")]
    public async Task CreateAsync_WorkspaceNotFoundForNonMember()
    {
        await using var ctx = _db.CreateContext();
        var (workspaceId, _) = await CreateWorkspaceWithAdminAsync(ctx);
        var outsider = await TestData.CreateUserAsync(ctx);

        var outcome = await NewService(ctx, new CapturingEmailSender()).CreateAsync(
            workspaceId, new CreateInvitationRequest("new@example.test", WorkspaceMemberRole.Member), outsider.Id);

        Assert.Equal(CreateInvitationResult.WorkspaceNotFound, outcome.Result);
    }

    [Fact(DisplayName = "既にメンバーのメール宛ての招待は AlreadyMember")]
    public async Task CreateAsync_AlreadyMember()
    {
        await using var ctx = _db.CreateContext();
        var (workspaceId, admin) = await CreateWorkspaceWithAdminAsync(ctx);
        var existing = await TestData.CreateUserAsync(ctx);
        await TestData.AddWorkspaceMemberAsync(ctx, workspaceId, existing.Id, WorkspaceMemberRole.Member);

        var outcome = await NewService(ctx, new CapturingEmailSender()).CreateAsync(
            workspaceId, new CreateInvitationRequest(existing.Email, WorkspaceMemberRole.Member), admin.Id);

        Assert.Equal(CreateInvitationResult.AlreadyMember, outcome.Result);
    }

    [Fact(DisplayName = "招待発行でトークンはハッシュ保存され、メールが送られる")]
    public async Task CreateAsync_StoresHashedTokenAndSendsEmail()
    {
        await using var ctx = _db.CreateContext();
        var (workspaceId, admin) = await CreateWorkspaceWithAdminAsync(ctx);
        var email = new CapturingEmailSender();

        var outcome = await NewService(ctx, email).CreateAsync(
            workspaceId, new CreateInvitationRequest("invitee@example.test", WorkspaceMemberRole.Viewer), admin.Id);

        Assert.Equal(CreateInvitationResult.Success, outcome.Result);
        Assert.Equal("invitee@example.test", email.To);

        var token = ExtractToken(email.Body!);
        await using var assert = _db.CreateContext();
        var stored = await assert.Invitations.SingleAsync(i => i.Id == outcome.Data!.Id);
        Assert.Equal(Hash(token), stored.TokenHash);     // 平文ではなくハッシュで保存
        Assert.NotEqual(token, stored.TokenHash);
        Assert.Null(stored.AcceptedAt);
        Assert.Equal(WorkspaceMemberRole.Viewer, stored.Role);
    }

    [Fact(DisplayName = "有効な招待は確認でき、無効なトークンは null")]
    public async Task PreviewAsync_ValidAndInvalid()
    {
        await using var ctx = _db.CreateContext();
        var (workspaceId, admin) = await CreateWorkspaceWithAdminAsync(ctx);
        var email = new CapturingEmailSender();
        await NewService(ctx, email).CreateAsync(
            workspaceId, new CreateInvitationRequest("invitee@example.test", WorkspaceMemberRole.Member), admin.Id);
        var token = ExtractToken(email.Body!);

        var preview = await NewService(ctx, email).PreviewAsync(token);
        Assert.NotNull(preview);
        Assert.Equal(workspaceId, preview!.WorkspaceId);
        Assert.False(preview.IsExistingUser);

        Assert.Null(await NewService(ctx, email).PreviewAsync("invalid-token"));
    }

    [Fact(DisplayName = "既存ユーザーの受諾はメンバー追加され、アカウントは作成しない")]
    public async Task AcceptAsync_ExistingUser()
    {
        await using var ctx = _db.CreateContext();
        var (workspaceId, admin) = await CreateWorkspaceWithAdminAsync(ctx);
        var existing = await TestData.CreateUserAsync(ctx);
        var email = new CapturingEmailSender();
        await NewService(ctx, email).CreateAsync(
            workspaceId, new CreateInvitationRequest(existing.Email, WorkspaceMemberRole.Member), admin.Id);
        var token = ExtractToken(email.Body!);

        var outcome = await NewService(ctx, email).AcceptAsync(token, new AcceptInvitationRequest(null, null));

        Assert.Equal(AcceptInvitationResult.Success, outcome.Result);
        Assert.False(outcome.Data!.AccountCreated);

        await using var assert = _db.CreateContext();
        Assert.True(await assert.WorkspaceMembers
            .AnyAsync(wm => wm.WorkspaceId == workspaceId && wm.UserId == existing.Id));
        Assert.NotNull((await assert.Invitations.SingleAsync(i => i.Email == existing.Email)).AcceptedAt);
    }

    [Fact(DisplayName = "新規ユーザーの受諾は名前・パスワードが必須")]
    public async Task AcceptAsync_NewUserRequiresRegistration()
    {
        await using var ctx = _db.CreateContext();
        var (workspaceId, admin) = await CreateWorkspaceWithAdminAsync(ctx);
        var email = new CapturingEmailSender();
        await NewService(ctx, email).CreateAsync(
            workspaceId, new CreateInvitationRequest("brand-new@example.test", WorkspaceMemberRole.Member), admin.Id);
        var token = ExtractToken(email.Body!);

        var outcome = await NewService(ctx, email).AcceptAsync(token, new AcceptInvitationRequest(null, null));

        Assert.Equal(AcceptInvitationResult.RegistrationRequired, outcome.Result);
    }

    [Fact(DisplayName = "新規ユーザーの受諾でアカウント作成＋メンバー追加される")]
    public async Task AcceptAsync_NewUserCreatesAccount()
    {
        await using var ctx = _db.CreateContext();
        var (workspaceId, admin) = await CreateWorkspaceWithAdminAsync(ctx);
        var email = new CapturingEmailSender();
        var inviteeEmail = TestData.Unique("newbie") + "@example.test";
        await NewService(ctx, email).CreateAsync(
            workspaceId, new CreateInvitationRequest(inviteeEmail, WorkspaceMemberRole.Member), admin.Id);
        var token = ExtractToken(email.Body!);

        var outcome = await NewService(ctx, email).AcceptAsync(
            token, new AcceptInvitationRequest("新規 太郎", "Password123!"));

        Assert.Equal(AcceptInvitationResult.Success, outcome.Result);
        Assert.True(outcome.Data!.AccountCreated);

        await using var assert = _db.CreateContext();
        var user = await assert.Users.SingleAsync(u => u.Email == inviteeEmail);
        Assert.True(await assert.WorkspaceMembers
            .AnyAsync(wm => wm.WorkspaceId == workspaceId && wm.UserId == user.Id));
        Assert.NotEqual("Password123!", user.PasswordHash); // 平文ではなくハッシュで保存
    }

    [Fact(DisplayName = "受諾は一度きり(2回目は無効)")]
    public async Task AcceptAsync_IsOneTime()
    {
        await using var ctx = _db.CreateContext();
        var (workspaceId, admin) = await CreateWorkspaceWithAdminAsync(ctx);
        var existing = await TestData.CreateUserAsync(ctx);
        var email = new CapturingEmailSender();
        await NewService(ctx, email).CreateAsync(
            workspaceId, new CreateInvitationRequest(existing.Email, WorkspaceMemberRole.Member), admin.Id);
        var token = ExtractToken(email.Body!);

        Assert.Equal(AcceptInvitationResult.Success,
            (await NewService(ctx, email).AcceptAsync(token, new AcceptInvitationRequest(null, null))).Result);
        Assert.Equal(AcceptInvitationResult.Invalid,
            (await NewService(ctx, email).AcceptAsync(token, new AcceptInvitationRequest(null, null))).Result);
    }

    [Fact(DisplayName = "期限切れトークンの受諾は無効")]
    public async Task AcceptAsync_ExpiredIsInvalid()
    {
        await using var ctx = _db.CreateContext();
        var (workspaceId, admin) = await CreateWorkspaceWithAdminAsync(ctx);
        var token = "expired-token-sample";
        ctx.Invitations.Add(new Invitation
        {
            WorkspaceId = workspaceId,
            Email = "late@example.test",
            Role = WorkspaceMemberRole.Member,
            TokenHash = Hash(token),
            ExpiresAt = DateTime.SpecifyKind(DateTime.UtcNow.AddDays(-1), DateTimeKind.Unspecified),
            InvitedBy = admin.Id,
        });
        await ctx.SaveChangesAsync();

        var outcome = await NewService(ctx, new CapturingEmailSender())
            .AcceptAsync(token, new AcceptInvitationRequest(null, null));

        Assert.Equal(AcceptInvitationResult.Invalid, outcome.Result);
    }
}
