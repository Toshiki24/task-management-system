import type { CreateInitialAdminInput, SetupStatus } from "@/types/setup";

/**
 * 初回セットアップ状態を取得する(M5 §8)。
 * 200 以外(404=機能無効、503=状態不明、ネットワーク障害)は作成フォームを出さない方向に倒す。
 */
export async function getSetupStatus(): Promise<SetupStatus> {
  try {
    const res = await fetch("/api/bff/setup/status", { cache: "no-store" });
    if (res.status === 200) {
      const body = (await res.json()) as { setupRequired: boolean };
      return body.setupRequired ? "required" : "not-required";
    }
    // 404 は機能無効/未提供 → 通常ログインへ
    if (res.status === 404) {
      return "not-required";
    }
    // 503 等は状態不明
    return "unknown";
  } catch {
    return "unknown";
  }
}

/** 初回 System Admin を作成する。失敗時はサーバーのメッセージで例外を投げる。 */
export async function createInitialAdmin(input: CreateInitialAdminInput): Promise<void> {
  const res = await fetch("/api/bff/setup/admin", {
    method: "POST",
    headers: {
      "Content-Type": "application/json",
      // CSRF 対策(BFF が必須とする独自ヘッダー)
      "X-Requested-With": "XMLHttpRequest",
    },
    body: JSON.stringify(input),
  });

  if (!res.ok) {
    const body = await res.json().catch(() => null);
    const message =
      body?.message
      ?? body?.errors?.[Object.keys(body?.errors ?? {})[0]]?.[0]
      ?? "管理者の作成に失敗しました。";
    throw new Error(message);
  }
}
