import { type NextRequest } from "next/server";
import { callBackend } from "@/lib/server/backend";
import { rejectCrossSiteRequest } from "@/lib/server/csrf";
import { backendUnavailable, relayBackendResponse } from "@/lib/server/responses";

type RouteContext = { params: Promise<{ token: string }> };

/**
 * 招待の受諾。新規ユーザーもアクセスするため認証不要で中継する(トークンが本人確認を兼ねる)。
 * 更新系のため CSRF チェック(Origin＋独自ヘッダー)は行う。
 */
export async function POST(request: NextRequest, { params }: RouteContext) {
  const rejected = rejectCrossSiteRequest(request);
  if (rejected) {
    return rejected;
  }

  const { token } = await params;

  try {
    const response = await callBackend(`/invitations/${encodeURIComponent(token)}/accept`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: await request.text(),
    });
    return relayBackendResponse(response);
  } catch {
    return backendUnavailable();
  }
}
