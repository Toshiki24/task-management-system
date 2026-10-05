using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using TaskManagementSystem.Api.Data;
using TaskManagementSystem.Api.Dtos.Invitations;
using TaskManagementSystem.Api.Models;

namespace TaskManagementSystem.Api.Services;

/// <summary>
/// ワークスペース招待の発行・確認・受諾(Phase 2 M1 §4)。
/// 招待は必ず 1 つのワークスペースを対象にロール指定で発行し、受諾者はそのワークスペースにのみ参加する。
/// トークンは平文を保存せず SHA-256 ハッシュのみを保存する(RefreshToken と同方針)。
/// </summary>
public class InvitationService : IInvitationService
{
    // 新規アカウント作成時のパスワード最小長
    private const int MinPasswordLength = 8;

    private readonly AppDbContext _dbContext;
    private readonly TimeProvider _timeProvider;
    private readonly IEmailSender _emailSender;
    private readonly TimeSpan _lifetime;
    private readonly string _acceptUrlBase;

    public InvitationService(
        AppDbContext dbContext,
        TimeProvider timeProvider,
        IEmailSender emailSender,
        IConfiguration configuration)
    {
        _dbContext = dbContext;
        _timeProvider = timeProvider;
        _emailSender = emailSender;
        var section = configuration.GetSection("Invitation");
        _lifetime = TimeSpan.FromDays(section.GetValue("ExpiresDays", 7d));
        _acceptUrlBase = section.GetValue("AcceptUrlBase", "/invitations/accept?token=")!;
    }

    public async Task<CreateInvitationOutcome> CreateAsync(
        long workspaceId, CreateInvitationRequest request, long currentUserId)
    {
        var access = await _dbContext.ResolveWorkspaceAccessAsync(workspaceId, currentUserId);
        if (access is null || !access.Value.CanView)
        {
            return new CreateInvitationOutcome(CreateInvitationResult.WorkspaceNotFound);
        }

        if (!access.Value.IsAdmin)
        {
            return new CreateInvitationOutcome(CreateInvitationResult.Forbidden);
        }

        var email = request.Email!.Trim();

        // 既にそのワークスペースのメンバーなら招待不要
        var alreadyMember = await _dbContext.WorkspaceMembers
            .AnyAsync(wm => wm.WorkspaceId == workspaceId && wm.User.Email == email);
        if (alreadyMember)
        {
            return new CreateInvitationOutcome(CreateInvitationResult.AlreadyMember);
        }

        var token = GenerateToken();
        var invitation = new Invitation
        {
            WorkspaceId = workspaceId,
            Email = email,
            Role = request.Role!,
            TokenHash = Hash(token),
            ExpiresAt = Now() + _lifetime,
            InvitedBy = currentUserId,
        };

        _dbContext.Invitations.Add(invitation);
        await _dbContext.SaveChangesAsync();

        await SendInvitationEmailAsync(workspaceId, email, token);

        return new CreateInvitationOutcome(
            CreateInvitationResult.Success,
            new InvitationCreatedDto(invitation.Id, workspaceId, email, invitation.Role, invitation.ExpiresAt));
    }

    public async Task<InvitationPreviewDto?> PreviewAsync(string token)
    {
        var invitation = await FindValidAsync(token);
        if (invitation is null)
        {
            return null;
        }

        var isExistingUser = await _dbContext.Users.AnyAsync(u => u.Email == invitation.Email);

        return new InvitationPreviewDto(
            invitation.WorkspaceId,
            invitation.Workspace.Name,
            invitation.Email,
            invitation.Role,
            isExistingUser);
    }

    public async Task<AcceptInvitationOutcome> AcceptAsync(string token, AcceptInvitationRequest request)
    {
        var invitation = await FindValidAsync(token);
        if (invitation is null)
        {
            return new AcceptInvitationOutcome(AcceptInvitationResult.Invalid);
        }

        var user = await _dbContext.Users.SingleOrDefaultAsync(u => u.Email == invitation.Email);
        var accountCreated = false;

        await using var transaction = await _dbContext.Database.BeginTransactionAsync();

        if (user is null)
        {
            // 新規ユーザー: 名前とパスワードが必須
            if (string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrEmpty(request.Password)
                || request.Password.Length < MinPasswordLength)
            {
                return new AcceptInvitationOutcome(
                    AcceptInvitationResult.RegistrationRequired,
                    Message: $"アカウント作成には名前と{MinPasswordLength}文字以上のパスワードが必要です。");
            }

            user = new User
            {
                Name = request.Name.Trim(),
                Email = invitation.Email,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            };
            _dbContext.Users.Add(user);
            await _dbContext.SaveChangesAsync();
            accountCreated = true;
        }

        // 既に同ワークスペースのメンバーでなければ追加する(二重追加を防ぐ)
        var alreadyMember = await _dbContext.WorkspaceMembers
            .AnyAsync(wm => wm.WorkspaceId == invitation.WorkspaceId && wm.UserId == user.Id);
        if (!alreadyMember)
        {
            _dbContext.WorkspaceMembers.Add(new WorkspaceMember
            {
                WorkspaceId = invitation.WorkspaceId,
                UserId = user.Id,
                Role = invitation.Role,
            });
        }

        invitation.AcceptedAt = Now();
        await _dbContext.SaveChangesAsync();
        await transaction.CommitAsync();

        return new AcceptInvitationOutcome(
            AcceptInvitationResult.Success,
            new AcceptInvitationResultDto(invitation.WorkspaceId, user.Id, accountCreated));
    }

    // 有効な招待(未受諾・未失効)を返す。無効なら null(存在・失効・使用済みを区別しない)
    private async Task<Invitation?> FindValidAsync(string token)
    {
        var tokenHash = Hash(token);
        var invitation = await _dbContext.Invitations
            .Include(i => i.Workspace)
            .SingleOrDefaultAsync(i => i.TokenHash == tokenHash);

        if (invitation is null || invitation.AcceptedAt is not null || invitation.ExpiresAt <= Now())
        {
            return null;
        }

        return invitation;
    }

    private Task SendInvitationEmailAsync(long workspaceId, string email, string token)
    {
        var acceptUrl = $"{_acceptUrlBase}{token}";
        var body = $"ワークスペースに招待されました。次のリンクから参加してください(有効期限 {_lifetime.TotalDays:0} 日)。\n{acceptUrl}";
        return _emailSender.SendAsync(email, "ワークスペースへの招待", body);
    }

    private static string GenerateToken() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static string Hash(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token))).ToLowerInvariant();

    // 列は timestamp without time zone のため、値はUTCのまま Kind を Unspecified にして扱う(AppDbContext と同じ方針)
    private DateTime Now() => DateTime.SpecifyKind(_timeProvider.GetUtcNow().UtcDateTime, DateTimeKind.Unspecified);
}
