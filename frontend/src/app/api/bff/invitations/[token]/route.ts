import { type NextRequest } from "next/server";
import { callBackend } from "@/lib/server/backend";
import { backendUnavailable, relayBackendResponse } from "@/lib/server/responses";

type RouteContext = { params: Promise<{ token: string }> };

/**
 * 招待の確認(表示用)。新規ユーザーもアクセスするため認証不要で中継する。
 * トークンが本人確認を兼ねるため、Authorization は付けず共有シークレット(callBackend)のみで呼ぶ。
 */
export async function GET(_request: NextRequest, { params }: RouteContext) {
  const { token } = await params;

  try {
    const response = await callBackend(`/invitations/${encodeURIComponent(token)}`, { method: "GET" });
    return relayBackendResponse(response);
  } catch {
    return backendUnavailable();
  }
}
