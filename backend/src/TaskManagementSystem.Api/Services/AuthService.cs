using Microsoft.EntityFrameworkCore;
using TaskManagementSystem.Api.Data;
using TaskManagementSystem.Api.Dtos.Auth;
using TaskManagementSystem.Api.Dtos.Common;
using TaskManagementSystem.Api.Models;
using TaskManagementSystem.Api.Services.Mfa;

namespace TaskManagementSystem.Api.Services;

public class AuthService : IAuthService
{
    // メールアドレスが存在しない場合にも照合するダミーのハッシュ。
    // 照合時間を実際のユーザーと揃えるため、ユーザー登録時と同じコスト(BCrypt.Net の既定値11)で生成する。
    private static readonly string DummyPasswordHash = BCrypt.Net.BCrypt.HashPassword(Guid.NewGuid().ToString());

    private readonly AppDbContext _dbContext;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly ILoginAttemptLimiter _loginAttemptLimiter;
    private readonly IRefreshTokenService _refreshTokenService;
    private readonly IMfaService _mfaService;
    private readonly IMfaChallengeTokenService _mfaChallengeTokenService;

    public AuthService(
        AppDbContext dbContext,
        IJwtTokenService jwtTokenService,
        ILoginAttemptLimiter loginAttemptLimiter,
        IRefreshTokenService refreshTokenService,
        IMfaService mfaService,
        IMfaChallengeTokenService mfaChallengeTokenService)
    {
        _dbContext = dbContext;
        _jwtTokenService = jwtTokenService;
        _loginAttemptLimiter = loginAttemptLimiter;
        _refreshTokenService = refreshTokenService;
        _mfaService = mfaService;
        _mfaChallengeTokenService = mfaChallengeTokenService;
    }

    public async Task<LoginOutcome> LoginAsync(LoginRequest request, string? clientIp = null)
    {
        // 制限中は、パスワードが正しいかどうかに関わらず照合しない(総当たりを続けても結果が分からないようにする)
        var retryAfter = _loginAttemptLimiter.GetRetryAfter(request.Email, clientIp);
        if (retryAfter is not null)
        {
            return new LoginOutcome(LoginResult.TooManyAttempts, RetryAfter: retryAfter);
        }

        var user = await _dbContext.Users
            .SingleOrDefaultAsync(u => u.Email == request.Email);

        // ユーザーが存在しない場合もBCryptの照合を行い、応答時間の差からメールアドレスの登録有無を推測できないようにする
        var passwordMatches = BCrypt.Net.BCrypt.Verify(request.Password, user?.PasswordHash ?? DummyPasswordHash);

        if (user is null || !passwordMatches)
        {
            _loginAttemptLimiter.RecordFailure(request.Email, clientIp);
            return new LoginOutcome(LoginResult.InvalidCredentials);
        }

        _loginAttemptLimiter.Reset(request.Email);

        // MFA 有効ユーザーは、パスワード認証だけではセッションを発行せず、TOTP 検証ステップへ誘導する
        if (await _mfaService.IsEnabledAsync(user.Id))
        {
            var mfaToken = _mfaChallengeTokenService.Issue(user.Id);
            return new LoginOutcome(LoginResult.MfaRequired, MfaToken: mfaToken);
        }

        var refreshToken = await _refreshTokenService.IssueAsync(user.Id);
        return new LoginOutcome(LoginResult.Success, CreateResponse(user, refreshToken));
    }

    public async Task<MfaVerifyOutcome> VerifyMfaAsync(MfaVerifyRequest request)
    {
        var userId = _mfaChallengeTokenService.Validate(request.MfaToken);
        if (userId is null)
        {
            return new MfaVerifyOutcome(MfaVerifyResult.InvalidToken);
        }

        if (!await _mfaService.VerifyForLoginAsync(userId.Value, request.Code))
        {
            return new MfaVerifyOutcome(MfaVerifyResult.InvalidCode);
        }

        var user = await _dbContext.Users.SingleOrDefaultAsync(u => u.Id == userId.Value);
        if (user is null)
        {
            return new MfaVerifyOutcome(MfaVerifyResult.InvalidToken);
        }

        var refreshToken = await _refreshTokenService.IssueAsync(user.Id);
        return new MfaVerifyOutcome(MfaVerifyResult.Success, CreateResponse(user, refreshToken));
    }

    public async Task<LoginResponse?> RefreshAsync(RefreshTokenRequest request)
    {
        var rotated = await _refreshTokenService.RotateAsync(request.RefreshToken);
        return rotated is null ? null : CreateResponse(rotated.User, rotated.NewRefreshToken);
    }

    public Task LogoutAsync(RefreshTokenRequest request) => _refreshTokenService.RevokeAsync(request.RefreshToken);

    private LoginResponse CreateResponse(User user, string refreshToken)
    {
        var accessToken = _jwtTokenService.GenerateToken(user);
        return new LoginResponse(
            accessToken.Token,
            accessToken.ExpiresAt,
            refreshToken,
            new AuthUserDto(user.Id, user.Name, user.Email, user.IsSystemAdmin));
    }
}
