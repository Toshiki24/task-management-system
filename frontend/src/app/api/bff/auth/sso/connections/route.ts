import { callBackend } from "@/lib/server/backend";
import { backendUnavailable, relayBackendResponse } from "@/lib/server/responses";

/**
 * ログイン画面に出す有効な SSO 接続の一覧を返す(未ログインから呼ぶ公開 API)。
 * バックエンドの公開エンドポイントへ中継する。
 */
export async function GET() {
  try {
    const response = await callBackend("/auth/sso/connections", { method: "GET" });
    return relayBackendResponse(response);
  } catch {
    return backendUnavailable();
  }
}
