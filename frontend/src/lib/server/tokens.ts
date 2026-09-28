import type { IronSession } from "iron-session";
import { callBackend, type BackendTokenResponse } from "@/lib/server/backend";
import { isLoggedIn, type SessionData } from "@/lib/server/session";

/**
 * 有効期限の何ミリ秒前からアクセストークンを再発行するか。
 * BFFがAPIを呼び出している途中で期限切れにならないよう、少し早めに再発行する。
 */
const REFRESH_MARGIN_MS = 10_000;

export class BackendUnavailableError extends Error {}

export async function saveTokens(session: IronSession<SessionData>, tokens: BackendTokenResponse): Promise<void> {
  session.accessToken = tokens.accessToken;
  session.accessTokenExpiresAt = tokens.accessTokenExpiresAt;
  session.refreshToken = tokens.refreshToken;
  session.user = tokens.user;
  await session.save();
}

/**
 * APIの呼び出しに使えるアクセストークンを返す。
 * 期限切れが近い(または force 指定の)場合はリフレッシュトークンで再発行し、セッションCookieを更新する。
 * ログインしていない、またはリフレッシュトークンが無効(期限切れ・失効・再利用検知)の場合は、
 * セッションを破棄して null を返す。
 */
export async function getValidAccessToken(
  session: IronSession<SessionData>,
  { force = false }: { force?: boolean } = {},
): Promise<string | null> {
  if (!isLoggedIn(session)) {
    return null;
  }

  const expiresAt = Date.parse(session.accessTokenExpiresAt);
  if (!force && expiresAt - Date.now() > REFRESH_MARGIN_MS) {
    return session.accessToken;
  }

  let response: Response;
  try {
    response = await callBackend("/auth/refresh", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ refreshToken: session.refreshToken }),
    });
  } catch (error) {
    throw new BackendUnavailableError("バックエンドAPIに接続できませんでした。", { cause: error });
  }

  if (response.status === 401) {
    session.destroy();
    return null;
  }
  if (!response.ok) {
    // API側の一時的な障害でログアウトさせないよう、401以外の失敗ではセッションを残す
    throw new BackendUnavailableError(`トークンの再発行に失敗しました(HTTP ${response.status})。`);
  }

  const tokens: BackendTokenResponse = await response.json();
  await saveTokens(session, tokens);
  return tokens.accessToken;
}
