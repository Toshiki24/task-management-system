import type { CurrentUser } from "@/types/auth";

/**
 * BFFからバックエンドAPI(ASP.NET Core)を呼び出す。
 * APIのURLはサーバー側の環境変数でのみ扱い、ブラウザには公開しない。
 */
function apiBaseUrl(): string {
  const url = process.env.API_BASE_URL;
  if (!url) {
    throw new Error("環境変数 API_BASE_URL に、バックエンドAPIのベースURL(例: http://localhost:5000/api)を設定してください。");
  }
  return url.replace(/\/+$/, "");
}

export function callBackend(path: string, init?: RequestInit): Promise<Response> {
  return fetch(`${apiBaseUrl()}${path}`, {
    ...init,
    // 認証情報を含む応答をキャッシュしない
    cache: "no-store",
  });
}

/** バックエンドのログインAPI・リフレッシュAPIのレスポンス */
export interface BackendTokenResponse {
  accessToken: string;
  accessTokenExpiresAt: string;
  refreshToken: string;
  user: CurrentUser;
}
