import { apiFetch } from "@/lib/api";
import type { CurrentUser, LoginRequest, LoginResponse, LoginStepResult } from "@/types/auth";

/**
 * ログイン状態はBFFが暗号化Cookie(HttpOnly)で管理する。
 * ブラウザのJavaScriptからはトークンを読み書きできないため、ログイン状態の確認もBFFに問い合わせる。
 */

/**
 * ログイン一次認証。MFA 無効なら即ログイン(user)、有効ならコード入力を促す(mfaToken)。
 */
export async function login(request: LoginRequest): Promise<LoginStepResult> {
  return apiFetch<LoginStepResult>("/auth/login", {
    method: "POST",
    body: JSON.stringify(request),
  });
}

/** ログインの MFA 検証ステップ。成功するとログインが確定する。 */
export async function verifyMfa(mfaToken: string, code: string): Promise<CurrentUser> {
  const data = await apiFetch<LoginResponse>("/auth/mfa/verify", {
    method: "POST",
    body: JSON.stringify({ mfaToken, code }),
  });
  return data.user;
}

export async function logout(): Promise<void> {
  await apiFetch<void>("/auth/logout", { method: "POST" });
}

/** ログイン中のユーザーを返す。ログインしていない(セッションが無効な)場合は null */
export async function fetchCurrentUser(): Promise<CurrentUser | null> {
  const response = await fetch("/api/bff/auth/session", { cache: "no-store" });
  if (!response.ok) {
    return null;
  }
  return ((await response.json()) as LoginResponse).user;
}
