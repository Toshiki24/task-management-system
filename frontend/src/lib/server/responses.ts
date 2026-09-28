import { NextResponse } from "next/server";

export function unauthorized(): NextResponse {
  return NextResponse.json({ message: "認証が必要です。" }, { status: 401 });
}

export function backendUnavailable(): NextResponse {
  return NextResponse.json({ message: "サーバーに接続できませんでした。" }, { status: 502 });
}

/** バックエンドAPIのレスポンスを、ステータス・本文・必要なヘッダーだけを引き継いでブラウザに返す */
export async function relayBackendResponse(response: Response): Promise<NextResponse> {
  const headers = new Headers();
  for (const name of ["content-type", "retry-after"]) {
    const value = response.headers.get(name);
    if (value) {
      headers.set(name, value);
    }
  }

  // 204等の本文を持たないレスポンスには本文を付けられない
  const body = response.status === 204 || response.status === 304 ? null : await response.arrayBuffer();
  return new NextResponse(body, { status: response.status, headers });
}
