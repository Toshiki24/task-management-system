import { NextResponse, type NextRequest } from "next/server";
import { callBackend } from "@/lib/server/backend";

type RouteContext = { params: Promise<{ id: string }> };

/**
 * SSO 認可の開始。バックエンドから IdP への遷移先 URL を取得し、ブラウザをリダイレクトする。
 * redirect_uri はこの BFF のコールバック URL に固定する(IdP 側の登録値と一致させる)。
 * ブラウザのトップレベル遷移(リンククリック)で呼ばれる GET のため、CSRF チェックは不要。
 */
export async function GET(request: NextRequest, { params }: RouteContext): Promise<NextResponse> {
  const { id } = await params;
  const origin = request.nextUrl.origin;
  const redirectUri = `${origin}/api/bff/auth/sso/callback`;

  try {
    const response = await callBackend(
      `/auth/sso/${encodeURIComponent(id)}/start?redirectUri=${encodeURIComponent(redirectUri)}`,
      { method: "GET" },
    );
    if (!response.ok) {
      return NextResponse.redirect(new URL("/login?sso_error=start", origin));
    }

    const { authorizationUrl } = (await response.json()) as { authorizationUrl: string };
    return NextResponse.redirect(authorizationUrl);
  } catch {
    return NextResponse.redirect(new URL("/login?sso_error=unavailable", origin));
  }
}
