import { getOriginVerifySecret } from "@/lib/server/secrets";
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

export async function callBackend(path: string, init?: RequestInit): Promise<Response> {
  const headers = new Headers(init?.headers);

  // BFFからの呼び出しであることを示す共有シークレットを付ける(security-review-2.md SEC2-01、aws-architecture.md 5.3)。
  // 本番は Secrets Manager、ローカル/E2E は環境変数から読み込む(secrets.ts)。未設定なら付けない(ローカル開発を妨げない)。
  const originVerifySecret = await getOriginVerifySecret();
  if (originVerifySecret) {
    headers.set("X-Origin-Verify", originVerifySecret);
  }

  return fetch(`${apiBaseUrl()}${path}`, {
    ...init,
    headers,
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
