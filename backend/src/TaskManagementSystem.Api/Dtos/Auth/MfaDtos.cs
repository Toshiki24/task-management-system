using System.ComponentModel.DataAnnotations;

namespace TaskManagementSystem.Api.Dtos.Auth;

/// <summary>MFA の現在の状態。</summary>
public record MfaStatusDto(bool Enabled);

/// <summary>有効化開始時に返す登録情報。<paramref name="Secret"/> は手動入力用、<paramref name="OtpauthUri"/> は QR 用。</summary>
public record MfaEnrollmentDto(string Secret, string OtpauthUri);

/// <summary>確認・無効化・再発行で共通の、TOTP コード(またはリカバリコード)入力。</summary>
public record MfaCodeRequest(
    [Required(ErrorMessage = "コードは必須です。")] string Code
);

/// <summary>リカバリコード(発行時のみ平文で返す)。</summary>
public record MfaRecoveryCodesDto(IReadOnlyList<string> RecoveryCodes);

/// <summary>ログインの MFA 検証ステップの入力。</summary>
public record MfaVerifyRequest(
    [Required(ErrorMessage = "MFA トークンは必須です。")] string MfaToken,
    [Required(ErrorMessage = "コードは必須です。")] string Code
);

/// <summary>
/// MFA 有効ユーザーのログイン一次応答。セッション(トークン)はまだ発行せず、
/// 短命の <see cref="MfaToken"/> を返して検証ステップへ誘導する。
/// </summary>
public record MfaChallengeResponse(bool MfaRequired, string MfaToken);
