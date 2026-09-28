import type { NextConfig } from "next";

const isDevelopment = process.env.NODE_ENV !== "production";

/**
 * Content-Security-Policy(security-review.md 5.5)。
 * 画面が読み込めるリソースと通信先を自サイトに限定し、XSSが発生した場合の被害を抑える。
 */
const contentSecurityPolicy = [
  "default-src 'self'",
  // Next.js(App Router)はページの描画にインラインスクリプトを使うため 'unsafe-inline' を許可する。
  // nonce 方式にはmiddlewareが必要で、デプロイ先(Amplify)での対応が未確認のため見送った(security-review.md 5.5)。
  // 外部サイトのスクリプトの読み込みは引き続き禁止される。
  // 開発サーバーはホットリロードのために eval を使うため、開発時のみ 'unsafe-eval' も許可する
  `script-src 'self' 'unsafe-inline'${isDevelopment ? " 'unsafe-eval'" : ""}`,
  "style-src 'self' 'unsafe-inline'",
  "img-src 'self' data: blob:",
  "font-src 'self'",
  // 画面の通信先はBFF(同一オリジン)のみ。XSSで盗んだ情報を外部へ送信されるのを防ぐ
  `connect-src 'self'${isDevelopment ? " ws:" : ""}`,
  "object-src 'none'",
  "base-uri 'self'",
  "form-action 'self'",
  // 他サイトのiframeに埋め込ませない(クリックジャッキング対策)
  "frame-ancestors 'none'",
].join("; ");

const securityHeaders = [
  { key: "Content-Security-Policy", value: contentSecurityPolicy },
  { key: "X-Content-Type-Options", value: "nosniff" },
  // frame-ancestors に対応していない古いブラウザ向け
  { key: "X-Frame-Options", value: "DENY" },
  { key: "Referrer-Policy", value: "strict-origin-when-cross-origin" },
  { key: "Permissions-Policy", value: "camera=(), microphone=(), geolocation=()" },
];

const nextConfig: NextConfig = {
  // E2Eテストでは開発サーバー(.next)と衝突しないよう、別の出力先でビルド・起動する
  distDir: process.env.NEXT_DIST_DIR || ".next",
  // 使用しているフレームワーク(X-Powered-By: Next.js)を外部に知らせない
  poweredByHeader: false,
  async headers() {
    return [{ source: "/:path*", headers: securityHeaders }];
  },
};

export default nextConfig;
