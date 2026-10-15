using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using TaskManagementSystem.Api.Data;
using TaskManagementSystem.Api.Dtos.Auth;
using TaskManagementSystem.Api.Dtos.Sso;
using TaskManagementSystem.Api.Models;
using TaskManagementSystem.Api.Services.Git;
using TaskManagementSystem.Api.Services.Sso;

namespace TaskManagementSystem.Api.Services;

/// <summary>SSO(OIDC)の管理と認可フロー(Phase 2 M5 §7)。すべて LINQ/EF。</summary>
public class SsoService : ISsoService
{
    private static readonly TimeSpan AuthRequestTtl = TimeSpan.FromMinutes(10);

    private readonly AppDbContext _dbContext;
    private readonly IOidcClient _oidcClient;
    private readonly ISecretStore _secrets;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly IRefreshTokenService _refreshTokenService;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<SsoService> _logger;

    public SsoService(
        AppDbContext dbContext,
        IOidcClient oidcClient,
        ISecretStore secrets,
        IJwtTokenService jwtTokenService,
        IRefreshTokenService refreshTokenService,
        TimeProvider timeProvider,
        ILogger<SsoService> logger)
    {
        _dbContext = dbContext;
        _oidcClient = oidcClient;
        _secrets = secrets;
        _jwtTokenService = jwtTokenService;
        _refreshTokenService = refreshTokenService;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task<List<SsoConnectionDto>?> GetConnectionsAsync(long currentUserId)
    {
        if (!await _dbContext.IsSystemAdminAsync(currentUserId))
        {
            return null;
        }

        return await _dbContext.SsoConnections
            .OrderBy(c => c.Id)
            .Select(c => ToDto(c))
            .ToListAsync();
    }

    public async Task<SsoConnectionOutcome> CreateConnectionAsync(long currentUserId, CreateSsoConnectionRequest request)
    {
        if (!await _dbContext.IsSystemAdminAsync(currentUserId))
        {
            return new SsoConnectionOutcome(SsoConnectionResult.Forbidden);
        }

        if (!await ProvisionWorkspaceValidAsync(request.ProvisionWorkspaceId))
        {
            return new SsoConnectionOutcome(SsoConnectionResult.WorkspaceNotFound);
        }

        var connection = new SsoConnection
        {
            Name = request.Name.Trim(),
            Protocol = SsoProtocol.Oidc,
            Issuer = request.Issuer.Trim(),
            ClientId = request.ClientId.Trim(),
            SecretRef = string.IsNullOrWhiteSpace(request.SecretRef) ? null : request.SecretRef.Trim(),
            Scopes = NormalizeScopes(request.Scopes),
            AutoProvision = request.AutoProvision,
            ProvisionWorkspaceId = request.ProvisionWorkspaceId,
            ProvisionRole = request.AutoProvision ? request.ProvisionRole : null,
            IsEnabled = true,
        };

        _dbContext.SsoConnections.Add(connection);
        await _dbContext.SaveChangesAsync();

        await _dbContext.RecordAuditAsync(
            currentUserId, AuditActions.SsoConnectionCreated, AuditTargets.SsoConnection, connection.Id,
            metadata: new { connection.Name, connection.Issuer });

        return new SsoConnectionOutcome(SsoConnectionResult.Success, ToDto(connection));
    }

    public async Task<SsoConnectionOutcome> UpdateConnectionAsync(
        long currentUserId, long id, UpdateSsoConnectionRequest request)
    {
        if (!await _dbContext.IsSystemAdminAsync(currentUserId))
        {
            return new SsoConnectionOutcome(SsoConnectionResult.Forbidden);
        }

        var connection = await _dbContext.SsoConnections.FirstOrDefaultAsync(c => c.Id == id);
        if (connection is null)
        {
            return new SsoConnectionOutcome(SsoConnectionResult.NotFound);
        }

        if (!await ProvisionWorkspaceValidAsync(request.ProvisionWorkspaceId))
        {
            return new SsoConnectionOutcome(SsoConnectionResult.WorkspaceNotFound);
        }

        connection.Name = request.Name.Trim();
        connection.Issuer = request.Issuer.Trim();
        connection.ClientId = request.ClientId.Trim();
        // SecretRef は null なら変更しない。空文字なら消去する(public client 化)
        if (request.SecretRef is not null)
        {
            connection.SecretRef = string.IsNullOrWhiteSpace(request.SecretRef) ? null : request.SecretRef.Trim();
        }

        connection.Scopes = NormalizeScopes(request.Scopes);
        connection.AutoProvision = request.AutoProvision;
        connection.ProvisionWorkspaceId = request.ProvisionWorkspaceId;
        connection.ProvisionRole = request.AutoProvision ? request.ProvisionRole : null;
        connection.IsEnabled = request.IsEnabled;
        connection.UpdatedAt = Now();

        await _dbContext.SaveChangesAsync();

        await _dbContext.RecordAuditAsync(
            currentUserId, AuditActions.SsoConnectionUpdated, AuditTargets.SsoConnection, connection.Id,
            metadata: new { connection.Name, connection.IsEnabled });

        return new SsoConnectionOutcome(SsoConnectionResult.Success, ToDto(connection));
    }

    public async Task<DeleteSsoConnectionResult> DeleteConnectionAsync(long currentUserId, long id)
    {
        if (!await _dbContext.IsSystemAdminAsync(currentUserId))
        {
            return DeleteSsoConnectionResult.Forbidden;
        }

        var connection = await _dbContext.SsoConnections.FirstOrDefaultAsync(c => c.Id == id);
        if (connection is null)
        {
            return DeleteSsoConnectionResult.NotFound;
        }

        _dbContext.SsoConnections.Remove(connection);
        await _dbContext.SaveChangesAsync();

        await _dbContext.RecordAuditAsync(
            currentUserId, AuditActions.SsoConnectionDeleted, AuditTargets.SsoConnection, id,
            metadata: new { connection.Name });

        return DeleteSsoConnectionResult.Success;
    }

    public async Task<List<PublicSsoConnectionDto>> GetEnabledConnectionsAsync() =>
        await _dbContext.SsoConnections
            .Where(c => c.IsEnabled)
            .OrderBy(c => c.Id)
            .Select(c => new PublicSsoConnectionDto(c.Id, c.Name))
            .ToListAsync();

    public async Task<SsoStartOutcome> StartAsync(long connectionId, string redirectUri)
    {
        var connection = await _dbContext.SsoConnections
            .FirstOrDefaultAsync(c => c.Id == connectionId && c.IsEnabled);
        if (connection is null)
        {
            return new SsoStartOutcome(SsoStartResult.NotFound);
        }

        OidcEndpoints endpoints;
        try
        {
            endpoints = await _oidcClient.GetEndpointsAsync(connection.Issuer);
        }
        catch (OidcException ex)
        {
            _logger.LogWarning(ex, "SSO 開始のディスカバリに失敗しました 接続={ConnectionId}", connectionId);
            return new SsoStartOutcome(SsoStartResult.Error);
        }

        var state = GenerateUrlSafeToken(32);
        var nonce = GenerateUrlSafeToken(16);
        var codeVerifier = GenerateUrlSafeToken(32);
        var codeChallenge = Base64UrlEncode(SHA256.HashData(Encoding.ASCII.GetBytes(codeVerifier)));
        var now = Now();

        _dbContext.SsoAuthRequests.Add(new SsoAuthRequest
        {
            ConnectionId = connection.Id,
            StateHash = HashState(state),
            CodeVerifier = codeVerifier,
            Nonce = nonce,
            RedirectUri = redirectUri,
            ExpiresAt = now.Add(AuthRequestTtl),
        });
        await _dbContext.SaveChangesAsync();

        var authorizationUrl = QueryHelpers.AddQueryString(endpoints.AuthorizationEndpoint, new Dictionary<string, string?>
        {
            ["response_type"] = "code",
            ["client_id"] = connection.ClientId,
            ["redirect_uri"] = redirectUri,
            ["scope"] = connection.Scopes,
            ["state"] = state,
            ["nonce"] = nonce,
            ["code_challenge"] = codeChallenge,
            ["code_challenge_method"] = "S256",
        });

        return new SsoStartOutcome(SsoStartResult.Success, authorizationUrl);
    }

    public async Task<SsoCallbackOutcome> CompleteAsync(SsoCallbackRequest request)
    {
        var stateHash = HashState(request.State);
        var now = Now();

        var authRequest = await _dbContext.SsoAuthRequests
            .Include(r => r.Connection)
            .FirstOrDefaultAsync(r => r.StateHash == stateHash && r.ConsumedAt == null && r.ExpiresAt > now);
        if (authRequest is null)
        {
            return new SsoCallbackOutcome(SsoCallbackResult.InvalidState);
        }

        // リプレイ防止: state は 1 回使ったら即消費済みにする(以降の失敗でも再利用不可)
        authRequest.ConsumedAt = now;
        await _dbContext.SaveChangesAsync();

        var connection = authRequest.Connection;

        OidcUserInfo userInfo;
        try
        {
            var endpoints = await _oidcClient.GetEndpointsAsync(connection.Issuer);
            var clientSecret = await _secrets.GetSecretAsync(connection.SecretRef);
            var token = await _oidcClient.ExchangeCodeAsync(
                endpoints, connection.ClientId, clientSecret, request.Code, authRequest.RedirectUri,
                authRequest.CodeVerifier);
            userInfo = await _oidcClient.ValidateIdTokenAsync(
                endpoints, token.IdToken, connection.ClientId, authRequest.Nonce);
        }
        catch (OidcException ex)
        {
            _logger.LogWarning(ex, "SSO コールバックの検証に失敗しました 接続={ConnectionId}", connection.Id);
            return new SsoCallbackOutcome(SsoCallbackResult.Error);
        }

        // メールが無い/未検証のアカウントは突合できないため許可しない
        if (string.IsNullOrWhiteSpace(userInfo.Email) || !userInfo.EmailVerified)
        {
            return new SsoCallbackOutcome(SsoCallbackResult.NotProvisioned);
        }

        var email = userInfo.Email.Trim();
        var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Email == email);
        if (user is null)
        {
            if (!connection.AutoProvision)
            {
                return new SsoCallbackOutcome(SsoCallbackResult.NotProvisioned);
            }

            user = await ProvisionUserAsync(connection, email, userInfo.Name);
        }

        var refreshToken = await _refreshTokenService.IssueAsync(user.Id);
        var accessToken = _jwtTokenService.GenerateToken(user);
        var response = new LoginResponse(
            accessToken.Token, accessToken.ExpiresAt, refreshToken,
            new AuthUserDto(user.Id, user.Name, user.Email, user.IsSystemAdmin));

        await _dbContext.RecordAuditAsync(
            user.Id, AuditActions.SsoLogin, AuditTargets.User, user.Id,
            metadata: new { connectionId = connection.Id });

        return new SsoCallbackOutcome(SsoCallbackResult.Success, response);
    }

    private async Task<User> ProvisionUserAsync(SsoConnection connection, string email, string? name)
    {
        var user = new User
        {
            Name = string.IsNullOrWhiteSpace(name) ? email.Split('@')[0] : name.Trim(),
            Email = email,
            // SSO ユーザーはパスワードログインしないが、ハッシュ列は必須のためランダム値を入れる
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(Guid.NewGuid().ToString("N")),
            IsSystemAdmin = false,
        };
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();

        if (connection.ProvisionWorkspaceId is { } workspaceId)
        {
            _dbContext.WorkspaceMembers.Add(new WorkspaceMember
            {
                WorkspaceId = workspaceId,
                UserId = user.Id,
                Role = connection.ProvisionRole ?? WorkspaceMemberRole.Member,
            });
            await _dbContext.SaveChangesAsync();
        }

        await _dbContext.RecordAuditAsync(
            user.Id, AuditActions.SsoUserProvisioned, AuditTargets.User, user.Id,
            connection.ProvisionWorkspaceId,
            metadata: new { connectionId = connection.Id });

        return user;
    }

    private DateTime Now() => DateTime.SpecifyKind(_timeProvider.GetUtcNow().UtcDateTime, DateTimeKind.Unspecified);

    private async Task<bool> ProvisionWorkspaceValidAsync(long? workspaceId) =>
        workspaceId is not { } id || await _dbContext.Workspaces.AnyAsync(w => w.Id == id);

    private static string NormalizeScopes(string? scopes)
    {
        var trimmed = scopes?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            return "openid email profile";
        }

        // openid は OIDC で必須のため常に含める
        return trimmed.Split(' ', StringSplitOptions.RemoveEmptyEntries).Contains("openid")
            ? trimmed
            : $"openid {trimmed}";
    }

    private static SsoConnectionDto ToDto(SsoConnection c) => new(
        c.Id, c.Name, c.Protocol, c.Issuer, c.ClientId, c.Scopes, c.AutoProvision,
        c.ProvisionWorkspaceId, c.ProvisionRole, c.IsEnabled, !string.IsNullOrEmpty(c.SecretRef));

    private static string HashState(string state) =>
        Convert.ToHexString(SHA256.HashData(Encoding.ASCII.GetBytes(state))).ToLowerInvariant();

    private static string GenerateUrlSafeToken(int bytes) =>
        Base64UrlEncode(RandomNumberGenerator.GetBytes(bytes));

    private static string Base64UrlEncode(byte[] data) =>
        Convert.ToBase64String(data).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
