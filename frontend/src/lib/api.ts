/**
 * 画面からのAPI呼び出しはすべて同じオリジンのBFF(/api/bff)を経由する。
 * アクセストークンはBFFが暗号化Cookieの中で管理するため、ここでは扱わない(security-review.md 5.3)。
 */
const BFF_BASE_URL = "/api/bff";

interface ApiErrorBody {
  message?: string;
  errors?: { field: string; message: string }[];
}

export class ApiError extends Error {
  status: number;
  errors?: { field: string; message: string }[];

  constructor(message: string, status: number, errors?: ApiErrorBody["errors"]) {
    super(message);
    this.status = status;
    this.errors = errors;
  }
}

export async function apiFetch<T>(path: string, options?: RequestInit): Promise<T> {
  const response = await fetch(`${BFF_BASE_URL}${path}`, {
    ...options,
    headers: {
      "Content-Type": "application/json",
      // CSRF対策として、BFFは更新系リクエストにこのヘッダーを必須としている(security-review.md 5.4)
      "X-Requested-With": "XMLHttpRequest",
      ...options?.headers,
    },
  });

  if (!response.ok) {
    if (response.status === 401) {
      // ログイン画面自体での認証失敗(パスワード誤り等)は画面上にエラー表示するため遷移させない。
      // それ以外の画面での401はセッション切れとみなし、ログイン画面へ強制的に戻す。
      if (typeof window !== "undefined" && window.location.pathname !== "/login") {
        // apiFetchはReactコンポーネント外(通常の関数)からも呼ばれるためuseRouter()が使えない。
        // セッション切れ時は状態を確実にリセットしたいので、あえてハードナビゲーションにしている。
        window.location.assign("/login");
      }
    }

    const body: ApiErrorBody | null = await response.json().catch(() => null);
    throw new ApiError(
      body?.message ?? "エラーが発生しました。",
      response.status,
      body?.errors,
    );
  }

  if (response.status === 204) {
    return undefined as T;
  }

  return (await response.json()) as T;
}

/**
 * ApiErrorからユーザー表示用のメッセージを作る。
 * フィールド別エラー(errors)がある場合はそれらを結合し、なければトップレベルのmessageを使う。
 */
export function formatApiErrorMessage(err: unknown, fallback: string): string {
  if (err instanceof ApiError) {
    if (err.errors && err.errors.length > 0) {
      return err.errors.map((e) => e.message).join(" / ");
    }
    return err.message;
  }
  return fallback;
}
