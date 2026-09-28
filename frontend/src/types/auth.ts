export interface LoginRequest {
  email: string;
  password: string;
}

export interface CurrentUser {
  id: number;
  name: string;
  email: string;
}

/** BFFのログイン・セッション確認のレスポンス(トークンは含まない) */
export interface LoginResponse {
  user: CurrentUser;
}
