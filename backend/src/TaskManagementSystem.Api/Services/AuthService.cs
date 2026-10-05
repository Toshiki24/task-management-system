using Microsoft.EntityFrameworkCore;
using TaskManagementSystem.Api.Data;
using TaskManagementSystem.Api.Dtos.Auth;
using TaskManagementSystem.Api.Dtos.Common;
using TaskManagementSystem.Api.Models;

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

    public AuthService(
        AppDbContext dbContext,
        IJwtTokenService jwtTokenService,
        ILoginAttemptLimiter loginAttemptLimiter,
        IRefreshTokenService refreshTokenService)
    {
        _dbContext = dbContext;
        _jwtTokenService = jwtTokenService;
        _loginAttemptLimiter = loginAttemptLimiter;
        _refreshTokenService = refreshTokenService;
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

        var refreshToken = await _refreshTokenService.IssueAsync(user.Id);
        return new LoginOutcome(LoginResult.Success, CreateResponse(user, refreshToken));
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
