import { NextResponse, type NextRequest } from "next/server";
import { callBackend } from "@/lib/server/backend";
import { rejectCrossSiteRequest } from "@/lib/server/csrf";
import { getSession } from "@/lib/server/session";

/** ログアウト。リフレッシュトークンをAPI側で失効させ、セッションCookieを削除する */
export async function POST(request: NextRequest) {
  const rejected = rejectCrossSiteRequest(request);
  if (rejected) {
    return rejected;
  }

  const session = await getSession();
  if (session.refreshToken) {
    try {
      await callBackend("/auth/logout", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ refreshToken: session.refreshToken }),
      });
    } catch {
      // APIに接続できなくても、ブラウザ側のセッションは必ず削除する
    }
  }

  session.destroy();
  return new NextResponse(null, { status: 204 });
}
