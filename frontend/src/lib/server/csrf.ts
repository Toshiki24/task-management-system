import { NextResponse, type NextRequest } from "next/server";

/**
 * 画面からBFFへの更新系リクエストに付ける独自ヘッダー。
 * 独自ヘッダーを付けたクロスオリジンのリクエストはプリフライトが必要になり、
 * BFFはCORSを許可していないため、他サイトからは送信できない(security-review.md 5.4)。
 */
export const CSRF_HEADER_NAME = "X-Requested-With";
export const CSRF_HEADER_VALUE = "XMLHttpRequest";

const SAFE_METHODS = new Set(["GET", "HEAD", "OPTIONS"]);

/**
 * CSRF対策として、更新系リクエスト(POST / PUT / DELETE等)が自サイトの画面から送られたものかを確認する。
 * 問題があれば403のレスポンスを、問題がなければ null を返す。
 *
 * SameSite=Lax のCookieと合わせて、以下の2つを確認する(多層防御)。
 * - Origin ヘッダーが、リクエスト先(自サイト)のホストと一致すること
 * - 独自ヘッダー(X-Requested-With)が付いていること
 */
export function rejectCrossSiteRequest(request: NextRequest): NextResponse | null {
  if (SAFE_METHODS.has(request.method)) {
    return null;
  }

  const origin = request.headers.get("origin");
  // Amplify(CloudFront)等のプロキシ経由では、元のホスト名が x-forwarded-host に入る
  const host = request.headers.get("x-forwarded-host") ?? request.headers.get("host");
  let originHost: string | null = null;
  try {
    originHost = origin ? new URL(origin).host : null;
  } catch {
    originHost = null;
  }

  if (!originHost || !host || originHost !== host || request.headers.get(CSRF_HEADER_NAME) !== CSRF_HEADER_VALUE) {
    return NextResponse.json({ message: "不正なリクエストです。" }, { status: 403 });
  }

  return null;
}
