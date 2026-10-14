using TaskManagementSystem.Api.Dtos.Auth;

namespace TaskManagementSystem.Api.Services.Mfa;

public enum MfaBeginResult { Success, AlreadyEnabled }
public enum MfaConfirmResult { Success, NotStarted, InvalidCode, AlreadyEnabled }
public enum MfaDisableResult { Success, NotEnabled, InvalidCode }
public enum MfaRegenerateResult { Success, NotEnabled, InvalidCode }

public record MfaBeginOutcome(MfaBeginResult Result, MfaEnrollmentDto? Data = null);
public record MfaConfirmOutcome(MfaConfirmResult Result, MfaRecoveryCodesDto? Data = null);
public record MfaRegenerateOutcome(MfaRegenerateResult Result, MfaRecoveryCodesDto? Data = null);

public interface IMfaService
{
    Task<MfaStatusDto> GetStatusAsync(long userId);

    /// <summary>有効化を開始する(シークレット生成・pending 保存)。確認コード入力まで有効にはしない。</summary>
    Task<MfaBeginOutcome> BeginEnrollmentAsync(long userId);

    /// <summary>初回コードを検証して有効化し、リカバリコードを発行する。</summary>
    Task<MfaConfirmOutcome> ConfirmEnrollmentAsync(long userId, string code);

    /// <summary>本人がコードを入力して MFA を無効化する。</summary>
    Task<MfaDisableResult> DisableAsync(long userId, string code);

    /// <summary>本人がコードを入力してリカバリコードを再発行する。</summary>
    Task<MfaRegenerateOutcome> RegenerateRecoveryCodesAsync(long userId, string code);

    /// <summary>ログイン時の判定用: 対象ユーザーが MFA 有効か。</summary>
    Task<bool> IsEnabledAsync(long userId);

    /// <summary>ログインの MFA 検証: TOTP コードまたはリカバリコードを検証する(リカバリは消費する)。</summary>
    Task<bool> VerifyForLoginAsync(long userId, string code);
}
