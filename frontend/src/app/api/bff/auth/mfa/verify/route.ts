import { NextResponse, type NextRequest } from "next/server";
import { callBackend, type BackendTokenResponse } from "@/lib/server/backend";
import { rejectCrossSiteRequest } from "@/lib/server/csrf";
import { backendUnavailable, relayBackendResponse } from "@/lib/server/responses";
import { getSession } from "@/lib/server/session";
import { saveTokens } from "@/lib/server/tokens";

/**
 * ログインの MFA 検証ステップ(M5 §6)。
 * 一次認証で受け取った mfaToken と TOTP/リカバリコードをバックエンドで検証し、
 * 成功したらトークンを暗号化Cookieに保存して、ブラウザにはユーザー情報だけを返す。
 *
 * 専用ルートにしているのは、検証成功時にトークンをセッションへ保存する必要があるため。
 * 状態取得(GET /auth/mfa)など他の MFA エンドポイントは汎用プロキシ([...path])が中継する。
 */
export async function POST(request: NextRequest) {
  const rejected = rejectCrossSiteRequest(request);
  if (rejected) {
    return rejected;
  }

  let response: Response;
  try {
    response = await callBackend("/auth/mfa/verify", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: await request.text(),
    });
  } catch {
    return backendUnavailable();
  }

  if (!response.ok) {
    return relayBackendResponse(response);
  }

  const tokens: BackendTokenResponse = await response.json();
  const session = await getSession();
  await saveTokens(session, tokens);

  return NextResponse.json({ user: tokens.user });
}
