/** MFA(TOTP)関連の型(M5 §6)。 */

export interface MfaStatus {
  enabled: boolean;
}

export interface MfaEnrollment {
  /** 手動入力用の Base32 シークレット。 */
  secret: string;
  /** 認証アプリ登録用の otpauth URI(QR 化する)。 */
  otpauthUri: string;
}

export interface MfaRecoveryCodes {
  recoveryCodes: string[];
}
