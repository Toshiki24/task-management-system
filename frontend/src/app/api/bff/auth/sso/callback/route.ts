import { NextResponse, type NextRequest } from "next/server";
import { callBackend, type BackendTokenResponse } from "@/lib/server/backend";
import { getSession } from "@/lib/server/session";
import { saveTokens } from "@/lib/server/tokens";

/**
 * IdP からのコールバック。state/code をバックエンドへ渡して検証・トークン発行し、
 * 成功したら暗号化 Cookie にセッションを保存してアプリへ遷移する。
 * IdP からのトップレベル遷移(GET)で呼ばれる。CSRF 対策は OIDC の state が担う。
 */
export async function GET(request: NextRequest): Promise<NextResponse> {
  const origin = request.nextUrl.origin;
  const params = request.nextUrl.searchParams;
  const state = params.get("state");
  const code = params.get("code");
  const error = params.get("error");

  if (error || !state || !code) {
    return NextResponse.redirect(new URL("/login?sso_error=denied", origin));
  }

  try {
    const response = await callBackend("/auth/sso/callback", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ state, code }),
    });

    if (!response.ok) {
      return NextResponse.redirect(new URL("/login?sso_error=auth", origin));
    }

    const tokens = (await response.json()) as BackendTokenResponse;
    const session = await getSession();
    await saveTokens(session, tokens);

    return NextResponse.redirect(new URL("/projects", origin));
  } catch {
    return NextResponse.redirect(new URL("/login?sso_error=unavailable", origin));
  }
}
