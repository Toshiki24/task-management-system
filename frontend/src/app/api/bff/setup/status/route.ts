import { callBackend } from "@/lib/server/backend";
import { backendUnavailable, relayBackendResponse } from "@/lib/server/responses";

/**
 * 初回セットアップ状態の取得(M5 §8)。未ログインで呼ぶため bearer を付けずにバックエンドへ中継する
 * (汎用プロキシ [...path] はアクセストークンを要求するため専用ルートにしている)。
 */
export async function GET() {
  let response: Response;
  try {
    response = await callBackend("/setup/status", { method: "GET" });
  } catch {
    return backendUnavailable();
  }

  // 404(機能無効)・503(状態不明)もそのまま返す。フロントはこれらを「作成不可」として扱う。
  return relayBackendResponse(response);
}
