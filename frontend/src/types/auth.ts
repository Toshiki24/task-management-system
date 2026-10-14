export interface LoginRequest {
  email: string;
  password: string;
}

export interface CurrentUser {
  id: number;
  name: string;
  email: string;
  isSystemAdmin: boolean;
}

/** BFFのログイン・セッション確認のレスポンス(トークンは含まない) */
export interface LoginResponse {
  user: CurrentUser;
}

/**
 * ログイン一次認証の結果(M5 §6)。
 * MFA 無効なら user が返りログイン完了、有効なら mfaToken が返りコード入力へ進む。
 */
export type LoginStepResult =
  | { mfaRequired: false; user: CurrentUser }
  | { mfaRequired: true; mfaToken: string };
