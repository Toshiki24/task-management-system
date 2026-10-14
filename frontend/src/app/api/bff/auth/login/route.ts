import { NextResponse, type NextRequest } from "next/server";
import { callBackend, type BackendTokenResponse } from "@/lib/server/backend";
import { clientIp } from "@/lib/server/client-ip";
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

  // IP単位のログイン制限(パスワードスプレー対策)のため、実クライアントIPをAPIへ転送する(security-review-2.md SEC2-03)
  const headers: Record<string, string> = { "Content-Type": "application/json" };
  const ip = clientIp(request);
  if (ip) {
    headers["X-Client-IP"] = ip;
  }

  let response: Response;
  try {
    response = await callBackend("/auth/login", {
      method: "POST",
      headers,
      body: await request.text(),
    });
  } catch {
    return backendUnavailable();
  }

  // 認証失敗(401)・試行回数の上限(429)・入力エラー(400)は、APIのレスポンスをそのまま返す
  if (!response.ok) {
    return relayBackendResponse(response);
  }

  const body = (await response.json()) as BackendTokenResponse | { mfaRequired: true; mfaToken: string };

  // MFA 有効ユーザーは、まだセッション(トークン)を発行しない。
  // ブラウザには MFA 検証用の短命トークンだけを返し、コード入力ステップへ進ませる。
  if ("mfaRequired" in body && body.mfaRequired) {
    return NextResponse.json({ mfaRequired: true, mfaToken: body.mfaToken });
  }

  const tokens = body as BackendTokenResponse;
  const session = await getSession();
  await saveTokens(session, tokens);

  return NextResponse.json({ mfaRequired: false, user: tokens.user });
}
