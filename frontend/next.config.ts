import type { NextConfig } from "next";

const isDevelopment = process.env.NODE_ENV !== "production";

// Content-Security-Policy はリクエストごとに nonce を付けるため middleware.ts で設定する
// (security-review-2.md SEC2-10)。ここでは CSP 以外のセキュリティヘッダーを設定する。
const securityHeaders = [
  { key: "X-Content-Type-Options", value: "nosniff" },
  // frame-ancestors に対応していない古いブラウザ向け
  { key: "X-Frame-Options", value: "DENY" },
  { key: "Referrer-Policy", value: "strict-origin-when-cross-origin" },
  { key: "Permissions-Policy", value: "camera=(), microphone=(), geolocation=()" },
  // 本番環境では、ブラウザに以後HTTPSでのみ接続させる(HSTS。security-review.md SEC-08)。
  // HTTPで配信する開発サーバーでは付けない(ブラウザはHTTPで受け取ったHSTSを無視する)
  ...(isDevelopment ? [] : [{ key: "Strict-Transport-Security", value: "max-age=31536000; includeSubDomains" }]),
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
