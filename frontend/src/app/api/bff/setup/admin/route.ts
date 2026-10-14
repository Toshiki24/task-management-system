import { type NextRequest } from "next/server";
import { callBackend } from "@/lib/server/backend";
import { rejectCrossSiteRequest } from "@/lib/server/csrf";
import { backendUnavailable, relayBackendResponse } from "@/lib/server/responses";

/**
 * 初回 System Admin の作成(M5 §8)。未ログインで呼ぶため bearer は付けない。
 * 更新系のため、ログインと同じく CSRF 対策(同一サイト判定)を行う。トークンは発行しない。
 */
export async function POST(request: NextRequest) {
  const rejected = rejectCrossSiteRequest(request);
  if (rejected) {
    return rejected;
  }

  let response: Response;
  try {
    response = await callBackend("/setup/admin", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: await request.text(),
    });
  } catch {
    return backendUnavailable();
  }

  // 成功(201)もエラー(400/404/409/503)もそのまま返す
  return relayBackendResponse(response);
}
