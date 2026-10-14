namespace TaskManagementSystem.Api.Models;

/// <summary>
/// ユーザーの MFA(TOTP)設定(M5 §6)。ユーザーと 1:1。
/// シークレットは暗号化して保存し、DB 平文には置かない。
/// </summary>
public class UserMfa
{
    /// <summary>対象ユーザー(主キー兼外部キー)。</summary>
    public long UserId { get; set; }

    /// <summary>AES-GCM で暗号化した TOTP シークレット(Base32 を暗号化したもの)。</summary>
    public string SecretCiphertext { get; set; } = null!;

    /// <summary>確認(初回コード検証)が完了し、ログインで要求する状態か。</summary>
    public bool IsEnabled { get; set; }

    /// <summary>有効化(確認)した日時。未確認なら NULL。</summary>
    public DateTime? ConfirmedAt { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public User User { get; set; } = null!;

    public ICollection<MfaRecoveryCode> RecoveryCodes { get; set; } = new List<MfaRecoveryCode>();
}

/// <summary>
/// MFA リカバリコード(M5 §6)。認証アプリを失ったときの代替。
/// パスワードと同方針でハッシュ化して保存し、使用したら使用済みにする(1回限り)。
/// </summary>
public class MfaRecoveryCode
{
    public long Id { get; set; }

    public long UserId { get; set; }

    /// <summary>リカバリコードの SHA-256 ハッシュ(平文は発行時のみ本人に表示)。</summary>
    public string CodeHash { get; set; } = null!;

    /// <summary>使用済み日時。未使用なら NULL。</summary>
    public DateTime? UsedAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public UserMfa UserMfa { get; set; } = null!;
}
