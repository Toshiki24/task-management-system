import { NextResponse, type NextRequest } from "next/server";
import { callBackend } from "@/lib/server/backend";
import { rejectCrossSiteRequest } from "@/lib/server/csrf";
import { backendUnavailable, relayBackendResponse, unauthorized } from "@/lib/server/responses";
import { getSession } from "@/lib/server/session";
import { BackendUnavailableError, getValidAccessToken } from "@/lib/server/tokens";

type RouteContext = { params: Promise<{ path: string[] }> };

/**
 * 画面からのAPI呼び出しを、セッションCookie内のアクセストークンを付けてバックエンドAPIへ中継する。
 * 例: GET /api/bff/projects/1 → GET {API_BASE_URL}/projects/1
 */
async function proxy(request: NextRequest, { params }: RouteContext): Promise<NextResponse> {
  const rejected = rejectCrossSiteRequest(request);
  if (rejected) {
    return rejected;
  }

  const { path } = await params;
  // 認証API(ログイン・リフレッシュ・ログアウト)は専用のRoute Handlerでのみ扱い、
  // リフレッシュトークンをブラウザ経由で扱えないよう中継しない
  if (path[0] === "auth") {
    return NextResponse.json({ message: "指定されたAPIは存在しません。" }, { status: 404 });
  }

  const backendPath = `/${path.map(encodeURIComponent).join("/")}${request.nextUrl.search}`;
  const body = request.method === "GET" || request.method === "HEAD" ? undefined : await request.text();
  const session = await getSession();

  const send = (accessToken: string) =>
    callBackend(backendPath, {
      method: request.method,
      headers: {
        "Content-Type": request.headers.get("content-type") ?? "application/json",
        Authorization: `Bearer ${accessToken}`,
      },
      body,
    });

  try {
    const accessToken = await getValidAccessToken(session);
    if (!accessToken) {
      return unauthorized();
    }

    let response = await send(accessToken);

    // 期限内のはずのトークンが拒否された場合(署名鍵の変更等)は、一度だけ再発行して再試行する
    if (response.status === 401) {
      const refreshed = await getValidAccessToken(session, { force: true });
      if (!refreshed) {
        return unauthorized();
      }
      response = await send(refreshed);
    }

    return relayBackendResponse(response);
  } catch (error) {
    if (error instanceof BackendUnavailableError || error instanceof TypeError) {
      // TypeError: fetch 自体の失敗(APIに接続できない)
      return backendUnavailable();
    }
    throw error;
  }
}

export { proxy as GET, proxy as POST, proxy as PUT, proxy as DELETE, proxy as PATCH };
