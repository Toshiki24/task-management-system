import { cookies } from "next/headers";
import { getIronSession, type IronSession, type SessionOptions } from "iron-session";
import { getSessionSecret } from "@/lib/server/secrets";
import type { CurrentUser } from "@/types/auth";

/**
 * BFFのセッション(暗号化Cookie)。
 *
 * アクセストークンとリフレッシュトークンはこのCookieの中だけに保持し、ブラウザのJavaScriptには渡さない
 * (security-review.md 5.3 / 6.4)。Cookieは iron-session で暗号化するため、ブラウザ側で中身を読んだり
 * 書き換えたりすることはできない。
 *
 * このモジュールはサーバー(Route Handler)専用。クライアントコンポーネントから import しないこと。
 */
export interface SessionData {
  accessToken: string;
  /** アクセストークンの有効期限(ISO 8601、UTC) */
  accessTokenExpiresAt: string;
  refreshToken: string;
  user: CurrentUser;
}

export const SESSION_COOKIE_NAME = "tms_session";

/** リフレッシュトークンの有効期限(API側の RefreshToken:ExpiresDays と揃える) */
const SESSION_TTL_SECONDS = 7 * 24 * 60 * 60;

async function sessionOptions(): Promise<SessionOptions> {
  return {
    cookieName: SESSION_COOKIE_NAME,
    // 本番は Secrets Manager、ローカル/E2E は環境変数から読み込む(secrets.ts)
    password: await getSessionSecret(),
    ttl: SESSION_TTL_SECONDS,
    cookieOptions: {
      // JavaScriptから読み取れないようにする(XSSでトークンを盗まれないため)
      httpOnly: true,
      // 本番(HTTPS)ではHTTPS通信でのみ送信する。ローカル開発(http://localhost)でも動くよう開発時は外す
      secure: process.env.NODE_ENV === "production",
      // 他サイトから送られるPOST等にはCookieを付けない(CSRF対策の1つ。security-review.md 5.4)
      sameSite: "lax",
      path: "/",
    },
  };
}

export async function getSession(): Promise<IronSession<SessionData>> {
  return getIronSession<SessionData>(await cookies(), await sessionOptions());
}

export function isLoggedIn(session: IronSession<SessionData>): session is IronSession<SessionData> & SessionData {
  return !!session.refreshToken && !!session.accessToken && !!session.user;
}
