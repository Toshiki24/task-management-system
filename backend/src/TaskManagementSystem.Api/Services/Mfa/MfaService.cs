using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using TaskManagementSystem.Api.Data;
using TaskManagementSystem.Api.Dtos.Auth;
using TaskManagementSystem.Api.Models;

namespace TaskManagementSystem.Api.Services.Mfa;

/// <summary>MFA(TOTP)の有効化・検証・無効化・リカバリコード管理(M5 §6)。</summary>
public class MfaService : IMfaService
{
    public const int RecoveryCodeCount = 10;

    private readonly AppDbContext _dbContext;
    private readonly IMfaSecretProtector _protector;
    private readonly TimeProvider _timeProvider;
    private readonly string _issuer;

    public MfaService(
        AppDbContext dbContext,
        IMfaSecretProtector protector,
        TimeProvider timeProvider,
        IConfiguration configuration)
    {
        _dbContext = dbContext;
        _protector = protector;
        _timeProvider = timeProvider;
        _issuer = configuration["Mfa:Issuer"] ?? "TaskManagementSystem";
    }

    public async Task<MfaStatusDto> GetStatusAsync(long userId)
    {
        var enabled = await IsEnabledAsync(userId);
        return new MfaStatusDto(enabled);
    }

    public async Task<bool> IsEnabledAsync(long userId)
    {
        return await _dbContext.UserMfas.AnyAsync(m => m.UserId == userId && m.IsEnabled);
    }

    public async Task<MfaBeginOutcome> BeginEnrollmentAsync(long userId)
    {
        var user = await _dbContext.Users.SingleOrDefaultAsync(u => u.Id == userId);
        if (user is null)
        {
            // 認証済みユーザーの操作なので通常起きないが、防御的に扱う
            return new MfaBeginOutcome(MfaBeginResult.AlreadyEnabled);
        }

        var existing = await _dbContext.UserMfas
            .Include(m => m.RecoveryCodes)
            .SingleOrDefaultAsync(m => m.UserId == userId);
        if (existing is { IsEnabled: true })
        {
            return new MfaBeginOutcome(MfaBeginResult.AlreadyEnabled);
        }

        var secret = Totp.GenerateSecret();
        var ciphertext = _protector.Protect(secret);

        if (existing is null)
        {
            _dbContext.UserMfas.Add(new UserMfa
            {
                UserId = userId,
                SecretCiphertext = ciphertext,
                IsEnabled = false,
            });
        }
        else
        {
            // 未確認の登録が残っていれば、新しいシークレットで上書きしてやり直す
            existing.SecretCiphertext = ciphertext;
            existing.ConfirmedAt = null;
            _dbContext.MfaRecoveryCodes.RemoveRange(existing.RecoveryCodes);
        }

        await _dbContext.SaveChangesAsync();

        var uri = Totp.BuildUri(secret, _issuer, user.Email);
        return new MfaBeginOutcome(MfaBeginResult.Success, new MfaEnrollmentDto(secret, uri));
    }

    public async Task<MfaConfirmOutcome> ConfirmEnrollmentAsync(long userId, string code)
    {
        var mfa = await _dbContext.UserMfas.SingleOrDefaultAsync(m => m.UserId == userId);
        if (mfa is null)
        {
            return new MfaConfirmOutcome(MfaConfirmResult.NotStarted);
        }

        if (mfa.IsEnabled)
        {
            return new MfaConfirmOutcome(MfaConfirmResult.AlreadyEnabled);
        }

        var secret = _protector.Unprotect(mfa.SecretCiphertext);
        if (!Totp.Verify(secret, code, _timeProvider.GetUtcNow()))
        {
            return new MfaConfirmOutcome(MfaConfirmResult.InvalidCode);
        }

        mfa.IsEnabled = true;
        mfa.ConfirmedAt = Now();

        var codes = await ReplaceRecoveryCodesAsync(userId);
        await _dbContext.SaveChangesAsync();

        return new MfaConfirmOutcome(MfaConfirmResult.Success, new MfaRecoveryCodesDto(codes));
    }

    public async Task<MfaDisableResult> DisableAsync(long userId, string code)
    {
        var mfa = await _dbContext.UserMfas
            .Include(m => m.RecoveryCodes)
            .SingleOrDefaultAsync(m => m.UserId == userId && m.IsEnabled);
        if (mfa is null)
        {
            return MfaDisableResult.NotEnabled;
        }

        if (!await VerifyAsync(mfa, code, consumeRecovery: false))
        {
            return MfaDisableResult.InvalidCode;
        }

        _dbContext.MfaRecoveryCodes.RemoveRange(mfa.RecoveryCodes);
        _dbContext.UserMfas.Remove(mfa);
        await _dbContext.SaveChangesAsync();

        return MfaDisableResult.Success;
    }

    public async Task<MfaRegenerateOutcome> RegenerateRecoveryCodesAsync(long userId, string code)
    {
        var mfa = await _dbContext.UserMfas.SingleOrDefaultAsync(m => m.UserId == userId && m.IsEnabled);
        if (mfa is null)
        {
            return new MfaRegenerateOutcome(MfaRegenerateResult.NotEnabled);
        }

        if (!await VerifyAsync(mfa, code, consumeRecovery: false))
        {
            return new MfaRegenerateOutcome(MfaRegenerateResult.InvalidCode);
        }

        var codes = await ReplaceRecoveryCodesAsync(userId);
        await _dbContext.SaveChangesAsync();

        return new MfaRegenerateOutcome(MfaRegenerateResult.Success, new MfaRecoveryCodesDto(codes));
    }

    public async Task<bool> VerifyForLoginAsync(long userId, string code)
    {
        var mfa = await _dbContext.UserMfas
            .Include(m => m.RecoveryCodes)
            .SingleOrDefaultAsync(m => m.UserId == userId && m.IsEnabled);
        if (mfa is null)
        {
            return false;
        }

        var ok = await VerifyAsync(mfa, code, consumeRecovery: true);
        if (ok)
        {
            await _dbContext.SaveChangesAsync();
        }

        return ok;
    }

    /// <summary>TOTP を検証し、失敗時は未使用のリカバリコードを照合する(consumeRecovery 時は使用済みにする)。</summary>
    private async Task<bool> VerifyAsync(UserMfa mfa, string code, bool consumeRecovery)
    {
        var secret = _protector.Unprotect(mfa.SecretCiphertext);
        if (Totp.Verify(secret, code, _timeProvider.GetUtcNow()))
        {
            return true;
        }

        var codeHash = HashCode(code);
        var recovery = await _dbContext.MfaRecoveryCodes
            .Where(c => c.UserId == mfa.UserId && c.UsedAt == null && c.CodeHash == codeHash)
            .FirstOrDefaultAsync();
        if (recovery is null)
        {
            return false;
        }

        if (consumeRecovery)
        {
            recovery.UsedAt = Now();
        }

        return true;
    }

    private async Task<List<string>> ReplaceRecoveryCodesAsync(long userId)
    {
        var existing = await _dbContext.MfaRecoveryCodes.Where(c => c.UserId == userId).ToListAsync();
        _dbContext.MfaRecoveryCodes.RemoveRange(existing);

        var codes = new List<string>(RecoveryCodeCount);
        for (var i = 0; i < RecoveryCodeCount; i++)
        {
            var code = GenerateRecoveryCode();
            codes.Add(code);
            _dbContext.MfaRecoveryCodes.Add(new MfaRecoveryCode
            {
                UserId = userId,
                CodeHash = HashCode(code),
            });
        }

        return codes;
    }

    /// <summary>"abcd-efgh" 形式のリカバリコード(紛らわしい文字を除く Base32 系)。</summary>
    private static string GenerateRecoveryCode()
    {
        const string alphabet = "abcdefghjkmnpqrstuvwxyz23456789"; // 0/o/1/l/i を除く
        var chars = new char[9];
        var bytes = RandomNumberGenerator.GetBytes(8);
        var pos = 0;
        for (var i = 0; i < 8; i++)
        {
            if (i == 4)
            {
                chars[pos++] = '-';
            }

            chars[pos++] = alphabet[bytes[i] % alphabet.Length];
        }

        return new string(chars);
    }

    /// <summary>入力ゆらぎ(大文字・ハイフン有無・空白)を吸収してからハッシュ照合する。</summary>
    private static string NormalizeCode(string code) =>
        code.Trim().ToLowerInvariant().Replace("-", "").Replace(" ", "");

    private string HashCode(string code) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(NormalizeCode(code)))).ToLowerInvariant();

    private DateTime Now() => DateTime.SpecifyKind(_timeProvider.GetUtcNow().UtcDateTime, DateTimeKind.Unspecified);
}
