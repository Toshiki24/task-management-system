import { NextResponse, type NextRequest } from "next/server";
import { callBackend, type BackendTokenResponse } from "@/lib/server/backend";
import { rejectCrossSiteRequest } from "@/lib/server/csrf";
import { backendUnavailable, relayBackendResponse } from "@/lib/server/responses";
import { getSession } from "@/lib/server/session";
import { saveTokens } from "@/lib/server/tokens";

/**
 * ログイン。バックエンドのログインAPIで取得したトークンを暗号化Cookieに保存し、
 * ブラウザにはユーザー情報だけを返す(トークンは返さない)。
 */
export async function POST(request: NextRequest) {
  const rejected = rejectCrossSiteRequest(request);
  if (rejected) {
    return rejected;
  }

  let response: Response;
  try {
    response = await callBackend("/auth/login", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: await request.text(),
    });
  } catch {
    return backendUnavailable();
  }

  // 認証失敗(401)・試行回数の上限(429)・入力エラー(400)は、APIのレスポンスをそのまま返す
  if (!response.ok) {
    return relayBackendResponse(response);
  }

  const tokens: BackendTokenResponse = await response.json();
  const session = await getSession();
  await saveTokens(session, tokens);

  return NextResponse.json({ user: tokens.user });
}
