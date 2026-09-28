import { apiFetch } from "@/lib/api";
import type { CurrentUser, LoginRequest, LoginResponse } from "@/types/auth";

/**
 * ログイン状態はBFFが暗号化Cookie(HttpOnly)で管理する。
 * ブラウザのJavaScriptからはトークンを読み書きできないため、ログイン状態の確認もBFFに問い合わせる。
 */

export async function login(request: LoginRequest): Promise<CurrentUser> {
  const data = await apiFetch<LoginResponse>("/auth/login", {
    method: "POST",
    body: JSON.stringify(request),
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
