import { NextResponse, type NextRequest } from "next/server";

/**
 * Content-Security-Policy をリクエストごとの nonce 付きで設定する(security-review-2.md SEC2-10)。
 *
 * script-src から 'unsafe-inline' を外し、Next.js が生成するスクリプトには nonce を付与する。
 * nonce は Next.js がリクエスト側の CSP ヘッダーから読み取り、自前の <script> に適用する。
 * そこから読み込まれるチャンクは 'strict-dynamic' で許可する。
 * これにより、万一 XSS が発生しても攻撃者が注入したインラインスクリプトは実行されない。
 */
export function middleware(request: NextRequest) {
  const isDevelopment = process.env.NODE_ENV !== "production";
  const nonce = btoa(crypto.randomUUID());

  const contentSecurityPolicy = [
    "default-src 'self'",
    // Next.js のスクリプトに nonce を付与し、そこから読み込むチャンクは strict-dynamic で許可する。
    // 'unsafe-inline' は付けない(nonce 併用時は CSP3 ブラウザで無視されるため、明示的に外す)。
    // 開発サーバーはホットリロードで eval を使うため、開発時のみ 'unsafe-eval' を許可する。
    `script-src 'self' 'nonce-${nonce}' 'strict-dynamic'${isDevelopment ? " 'unsafe-eval'" : ""}`,
    // スタイルは Tailwind 等がインラインを使うため 'unsafe-inline' を許可する(nonce 化の対象外)
    "style-src 'self' 'unsafe-inline'",
    "img-src 'self' data: blob:",
    "font-src 'self'",
    // 画面の通信先は BFF(同一オリジン)のみ。XSS で盗んだ情報を外部へ送信されるのを防ぐ
    `connect-src 'self'${isDevelopment ? " ws:" : ""}`,
    "object-src 'none'",
    "base-uri 'self'",
    "form-action 'self'",
    // 他サイトの iframe に埋め込ませない(クリックジャッキング対策)
    "frame-ancestors 'none'",
  ].join("; ");

  // Next.js が nonce を読み取れるよう、リクエスト側ヘッダーにも CSP と nonce を載せる
  const requestHeaders = new Headers(request.headers);
  requestHeaders.set("x-nonce", nonce);
  requestHeaders.set("Content-Security-Policy", contentSecurityPolicy);

  const response = NextResponse.next({ request: { headers: requestHeaders } });
  // ブラウザに適用させるため、レスポンス側にも同じ CSP を設定する
  response.headers.set("Content-Security-Policy", contentSecurityPolicy);
  return response;
}

export const config = {
  matcher: [
    {
      // 静的アセット・画像最適化・favicon を除く全てに適用する。
      // プリフェッチ要求は除外する(実際のページ読み込み時に nonce を適用するため)。
      source: "/((?!_next/static|_next/image|favicon.ico).*)",
      missing: [
        { type: "header", key: "next-router-prefetch" },
        { type: "header", key: "purpose", value: "prefetch" },
      ],
    },
  ],
};
