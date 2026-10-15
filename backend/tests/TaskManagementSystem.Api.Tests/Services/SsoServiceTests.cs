using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using TaskManagementSystem.Api.Data;
using TaskManagementSystem.Api.Dtos.Sso;
using TaskManagementSystem.Api.Models;
using TaskManagementSystem.Api.Services;
using TaskManagementSystem.Api.Services.Git;
using TaskManagementSystem.Api.Services.Sso;
using TaskManagementSystem.Api.Tests.Infrastructure;

namespace TaskManagementSystem.Api.Tests.Services;

public class SsoServiceTests : IClassFixture<TestDatabaseFixture>
{
    private readonly TestDatabaseFixture _db;

    public SsoServiceTests(TestDatabaseFixture db)
    {
        _db = db;
    }

    private sealed class FakeOidcClient : IOidcClient
    {
        public OidcEndpoints Endpoints { get; set; } = new(
            "https://idp.example.com", "https://idp.example.com/authorize",
            "https://idp.example.com/token", "https://idp.example.com/jwks");

        public OidcUserInfo UserInfo { get; set; } = new("sub-1", "sso-user@example.test", true, "SSO User");
        public bool ThrowOnValidate { get; set; }
        public string? CapturedCodeVerifier { get; private set; }
        public string? CapturedRedirectUri { get; private set; }

        public Task<OidcEndpoints> GetEndpointsAsync(string issuer, CancellationToken ct = default) =>
            Task.FromResult(Endpoints);

        public Task<OidcTokenResult> ExchangeCodeAsync(
            OidcEndpoints endpoints, string clientId, string? clientSecret, string code, string redirectUri,
            string codeVerifier, CancellationToken ct = default)
        {
            CapturedCodeVerifier = codeVerifier;
            CapturedRedirectUri = redirectUri;
            return Task.FromResult(new OidcTokenResult("id-token", null));
        }

        public Task<OidcUserInfo> ValidateIdTokenAsync(
            OidcEndpoints endpoints, string idToken, string clientId, string expectedNonce,
            CancellationToken ct = default)
        {
            if (ThrowOnValidate)
            {
                throw new OidcException("検証失敗");
            }

            return Task.FromResult(UserInfo);
        }
    }

    private sealed class FakeJwtTokenService : IJwtTokenService
    {
        public AccessToken GenerateToken(User user) => new("access-token", DateTime.UtcNow.AddMinutes(15));
    }

    private sealed class FakeRefreshTokenService : IRefreshTokenService
    {
        public Task<string> IssueAsync(long userId) => Task.FromResult($"refresh-{userId}");
        public Task<RotatedRefreshToken?> RotateAsync(string refreshToken) =>
            Task.FromResult<RotatedRefreshToken?>(null);
        public Task RevokeAsync(string refreshToken) => Task.CompletedTask;
    }

    private sealed class RefSecretStore : ISecretStore
    {
        public Task<string?> GetSecretAsync(string? secretRef, CancellationToken ct = default) =>
            Task.FromResult(secretRef);
    }

    private static SsoService NewService(AppDbContext ctx, FakeOidcClient oidc) => new(
        ctx, oidc, new RefSecretStore(), new FakeJwtTokenService(), new FakeRefreshTokenService(),
        TimeProvider.System, NullLogger<SsoService>.Instance);

    private static async Task<User> AdminAsync(AppDbContext ctx)
    {
        var admin = await TestData.CreateUserAsync(ctx);
        admin.IsSystemAdmin = true;
        await ctx.SaveChangesAsync();
        return admin;
    }

    private static SsoConnection AddConnection(AppDbContext ctx, bool autoProvision = false, long? wsId = null)
    {
        var connection = new SsoConnection
        {
            Name = TestData.Unique("idp"),
            Issuer = "https://idp.example.com",
            ClientId = "client-123",
            SecretRef = "oidc-secret",
            AutoProvision = autoProvision,
            ProvisionWorkspaceId = wsId,
            ProvisionRole = autoProvision && wsId is not null ? WorkspaceMemberRole.Member : null,
        };
        ctx.SsoConnections.Add(connection);
        ctx.SaveChanges();
        return connection;
    }

    private static string StateFromUrl(string url) =>
        QueryHelpers.ParseQuery(new Uri(url).Query)["state"].ToString();

    [Fact(DisplayName = "SSO 接続の CRUD は System Admin 以外は Forbidden/null")]
    public async Task Crud_ForbiddenForNonAdmin()
    {
        await using var ctx = _db.CreateContext();
        var user = await TestData.CreateUserAsync(ctx);
        var service = NewService(ctx, new FakeOidcClient());

        Assert.Null(await service.GetConnectionsAsync(user.Id));
        var create = await service.CreateConnectionAsync(user.Id,
            new CreateSsoConnectionRequest("x", "https://idp", "c", null, null, false, null, null));
        Assert.Equal(SsoConnectionResult.Forbidden, create.Result);
        Assert.Equal(DeleteSsoConnectionResult.Forbidden, await service.DeleteConnectionAsync(user.Id, 1));
    }

    [Fact(DisplayName = "SSO 接続を作成・更新・削除でき、シークレット値は返さない")]
    public async Task Crud_HappyPath()
    {
        await using var ctx = _db.CreateContext();
        var admin = await AdminAsync(ctx);
        var service = NewService(ctx, new FakeOidcClient());

        var created = await service.CreateConnectionAsync(admin.Id, new CreateSsoConnectionRequest(
            "Google", "https://accounts.google.com", "client-abc", "my-secret-ref", "openid email", false, null, null));
        Assert.Equal(SsoConnectionResult.Success, created.Result);
        Assert.True(created.Data!.HasSecret);
        Assert.Equal("openid email", created.Data.Scopes); // openid を含むのでそのまま
        var id = created.Data.Id;

        var updated = await service.UpdateConnectionAsync(admin.Id, id, new UpdateSsoConnectionRequest(
            "Google Workspace", "https://accounts.google.com", "client-abc", null, "email profile", true, null, null,
            false));
        Assert.Equal(SsoConnectionResult.Success, updated.Result);
        Assert.Equal("Google Workspace", updated.Data!.Name);
        Assert.StartsWith("openid ", updated.Data.Scopes); // openid を自動補完
        Assert.False(updated.Data.IsEnabled);
        Assert.True(updated.Data.HasSecret); // SecretRef=null は変更なし

        Assert.Equal(DeleteSsoConnectionResult.Success, await service.DeleteConnectionAsync(admin.Id, id));
        Assert.Equal(DeleteSsoConnectionResult.NotFound, await service.DeleteConnectionAsync(admin.Id, id));
    }

    [Fact(DisplayName = "存在しないプロビジョニング先WSは WorkspaceNotFound")]
    public async Task Create_RejectsMissingProvisionWorkspace()
    {
        await using var ctx = _db.CreateContext();
        var admin = await AdminAsync(ctx);
        var service = NewService(ctx, new FakeOidcClient());

        var result = await service.CreateConnectionAsync(admin.Id, new CreateSsoConnectionRequest(
            "x", "https://idp", "c", null, null, true, TestData.NonExistentId, "MEMBER"));
        Assert.Equal(SsoConnectionResult.WorkspaceNotFound, result.Result);
    }

    [Fact(DisplayName = "有効な接続のみ公開一覧に出る")]
    public async Task GetEnabled_ReturnsOnlyEnabled()
    {
        await using var ctx = _db.CreateContext();
        var enabled = AddConnection(ctx);
        var disabled = AddConnection(ctx);
        disabled.IsEnabled = false;
        await ctx.SaveChangesAsync();

        var list = await NewService(ctx, new FakeOidcClient()).GetEnabledConnectionsAsync();
        Assert.Contains(list, c => c.Id == enabled.Id);
        Assert.DoesNotContain(list, c => c.Id == disabled.Id);
    }

    [Fact(DisplayName = "開始は認可URLを返し、state/PKCE/nonce を保存する")]
    public async Task Start_BuildsUrlAndPersistsState()
    {
        await using var ctx = _db.CreateContext();
        var connection = AddConnection(ctx);
        var redirect = "https://app.example.com/api/bff/auth/sso/callback";

        var outcome = await NewService(ctx, new FakeOidcClient()).StartAsync(connection.Id, redirect);

        Assert.Equal(SsoStartResult.Success, outcome.Result);
        var query = QueryHelpers.ParseQuery(new Uri(outcome.AuthorizationUrl!).Query);
        Assert.Equal("code", query["response_type"]);
        Assert.Equal(connection.ClientId, query["client_id"]);
        Assert.Equal(redirect, query["redirect_uri"]);
        Assert.Equal("S256", query["code_challenge_method"]);
        Assert.False(string.IsNullOrEmpty(query["state"]));
        Assert.False(string.IsNullOrEmpty(query["code_challenge"]));

        await using var assert = _db.CreateContext();
        Assert.True(await assert.SsoAuthRequests.AnyAsync(r => r.ConnectionId == connection.Id && r.ConsumedAt == null));
    }

    [Fact(DisplayName = "無効/不明な接続の開始は NotFound")]
    public async Task Start_NotFoundForDisabled()
    {
        await using var ctx = _db.CreateContext();
        var outcome = await NewService(ctx, new FakeOidcClient()).StartAsync(TestData.NonExistentId, "https://app/cb");
        Assert.Equal(SsoStartResult.NotFound, outcome.Result);
    }

    [Fact(DisplayName = "コールバック: 既存ユーザーに突合してトークンを発行する(PKCE 使用)")]
    public async Task Complete_ExistingUserLogsIn()
    {
        await using var ctx = _db.CreateContext();
        var connection = AddConnection(ctx);
        var user = await TestData.CreateUserAsync(ctx);
        var oidc = new FakeOidcClient { UserInfo = new OidcUserInfo("sub", user.Email, true, user.Name) };
        var service = NewService(ctx, oidc);

        var start = await service.StartAsync(connection.Id, "https://app/cb");
        var state = StateFromUrl(start.AuthorizationUrl!);

        var outcome = await service.CompleteAsync(new SsoCallbackRequest(state, "auth-code"));

        Assert.Equal(SsoCallbackResult.Success, outcome.Result);
        Assert.Equal(user.Id, outcome.Response!.User.Id);
        Assert.Equal("access-token", outcome.Response.AccessToken);
        Assert.False(string.IsNullOrEmpty(oidc.CapturedCodeVerifier)); // PKCE が渡っている
        Assert.Equal("https://app/cb", oidc.CapturedRedirectUri); // 保存した redirect_uri を使う
    }

    [Fact(DisplayName = "コールバック: state は 1 回限り(リプレイは InvalidState)")]
    public async Task Complete_StateIsSingleUse()
    {
        await using var ctx = _db.CreateContext();
        var connection = AddConnection(ctx);
        var user = await TestData.CreateUserAsync(ctx);
        var service = NewService(ctx, new FakeOidcClient { UserInfo = new OidcUserInfo("s", user.Email, true, "n") });

        var start = await service.StartAsync(connection.Id, "https://app/cb");
        var state = StateFromUrl(start.AuthorizationUrl!);

        Assert.Equal(SsoCallbackResult.Success, (await service.CompleteAsync(new SsoCallbackRequest(state, "c"))).Result);
        Assert.Equal(SsoCallbackResult.InvalidState,
            (await service.CompleteAsync(new SsoCallbackRequest(state, "c"))).Result);
    }

    [Fact(DisplayName = "コールバック: 不正な state は InvalidState")]
    public async Task Complete_InvalidState()
    {
        await using var ctx = _db.CreateContext();
        var outcome = await NewService(ctx, new FakeOidcClient())
            .CompleteAsync(new SsoCallbackRequest("no-such-state", "c"));
        Assert.Equal(SsoCallbackResult.InvalidState, outcome.Result);
    }

    [Fact(DisplayName = "コールバック: 未登録メール×自動作成オフは NotProvisioned")]
    public async Task Complete_UnknownEmailWithoutAutoProvision()
    {
        await using var ctx = _db.CreateContext();
        var connection = AddConnection(ctx, autoProvision: false);
        var service = NewService(ctx,
            new FakeOidcClient { UserInfo = new OidcUserInfo("s", $"new-{TestData.Unique("u")}@example.test", true, "N") });

        var start = await service.StartAsync(connection.Id, "https://app/cb");
        var outcome = await service.CompleteAsync(new SsoCallbackRequest(StateFromUrl(start.AuthorizationUrl!), "c"));

        Assert.Equal(SsoCallbackResult.NotProvisioned, outcome.Result);
    }

    [Fact(DisplayName = "コールバック: 未登録メール×自動作成オンはユーザーとWSメンバーを作成する")]
    public async Task Complete_AutoProvisionsUser()
    {
        await using var ctx = _db.CreateContext();
        var workspace = await TestData.CreateWorkspaceAsync(ctx);
        var connection = AddConnection(ctx, autoProvision: true, wsId: workspace.Id);
        var email = $"provisioned-{TestData.Unique("u")}@example.test";
        var service = NewService(ctx, new FakeOidcClient { UserInfo = new OidcUserInfo("s", email, true, "New User") });

        var start = await service.StartAsync(connection.Id, "https://app/cb");
        var outcome = await service.CompleteAsync(new SsoCallbackRequest(StateFromUrl(start.AuthorizationUrl!), "c"));

        Assert.Equal(SsoCallbackResult.Success, outcome.Result);

        await using var assert = _db.CreateContext();
        var created = await assert.Users.SingleAsync(u => u.Email == email);
        Assert.Equal("New User", created.Name);
        Assert.True(await assert.WorkspaceMembers.AnyAsync(
            m => m.WorkspaceId == workspace.Id && m.UserId == created.Id && m.Role == WorkspaceMemberRole.Member));
    }

    [Fact(DisplayName = "コールバック: メール未検証は NotProvisioned")]
    public async Task Complete_RejectsUnverifiedEmail()
    {
        await using var ctx = _db.CreateContext();
        var connection = AddConnection(ctx, autoProvision: true);
        var service = NewService(ctx,
            new FakeOidcClient { UserInfo = new OidcUserInfo("s", "x@example.test", EmailVerified: false, "N") });

        var start = await service.StartAsync(connection.Id, "https://app/cb");
        var outcome = await service.CompleteAsync(new SsoCallbackRequest(StateFromUrl(start.AuthorizationUrl!), "c"));

        Assert.Equal(SsoCallbackResult.NotProvisioned, outcome.Result);
    }

    [Fact(DisplayName = "コールバック: OIDC 検証失敗は Error")]
    public async Task Complete_OidcFailureIsError()
    {
        await using var ctx = _db.CreateContext();
        var connection = AddConnection(ctx);
        var service = NewService(ctx, new FakeOidcClient { ThrowOnValidate = true });

        var start = await service.StartAsync(connection.Id, "https://app/cb");
        var outcome = await service.CompleteAsync(new SsoCallbackRequest(StateFromUrl(start.AuthorizationUrl!), "c"));

        Assert.Equal(SsoCallbackResult.Error, outcome.Result);
    }
}
