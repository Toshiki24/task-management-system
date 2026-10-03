import type { NextRequest } from "next/server";

/**
 * リクエストから実クライアントのIPアドレスを取り出す(IP単位のログイン制限に使う。security-review-2.md SEC2-03)。
 *
 * 本番(Amplify / CloudFront)では `x-forwarded-for` に実クライアントのIPが入る。
 * ローカル開発ではプロキシを経由しないため通常 null になり、IP単位の制限は行われない。
 *
 * 注意: `x-forwarded-for` はプロキシ構成によって信頼できる位置が異なる(クライアントが詐称しうる)。
 * 本対策は多層防御の一つ(メール単位の制限・API Gateway のスロットリングと併用)であり、
 * デプロイ時に CloudFront が付与する値の位置を確認すること(security-review-2.md SEC2-03 の補足)。
 */
export function clientIp(request: NextRequest): string | null {
  const forwardedFor = request.headers.get("x-forwarded-for");
  if (forwardedFor) {
    const first = forwardedFor.split(",")[0]?.trim();
    if (first) {
      return first;
    }
  }

  return request.headers.get("x-real-ip")?.trim() || null;
}
