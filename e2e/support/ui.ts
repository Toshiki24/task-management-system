import { expect, type Locator, type Page, type Request } from "@playwright/test";
import { WEB_URL } from "./env";
import type { TestUser } from "./fixtures";

/** 画面からBFFへの更新系リクエストに付くヘッダー(CSRF対策のため、BFFはこれがないリクエストを拒否する) */
export const BFF_HEADERS = { Origin: WEB_URL, "X-Requested-With": "XMLHttpRequest" };

/**
 * ログイン画面を操作せずに、ログイン済みの状態(BFFのセッションCookie)を作る。
 * page.request はブラウザとCookieを共有するため、BFFのログインAPIを呼ぶとブラウザにもCookieが保存される。
 */
export async function signIn(page: Page, user: TestUser): Promise<void> {
  const response = await page.request.post("/api/bff/auth/login", {
    headers: BFF_HEADERS,
    data: { email: user.email, password: user.password },
  });
  expect(response.status(), "BFF経由でログインできること").toBe(200);
}

/** ログイン画面から実際にログインする */
export async function loginViaUi(page: Page, user: TestUser): Promise<void> {
  await page.goto("/login");
  await page.getByLabel("メールアドレス").fill(user.email);
  await page.getByLabel("パスワード").fill(user.password);
  await page.getByRole("button", { name: "ログイン" }).click();
  await page.waitForURL("/projects");
}

/** タイトルで指定した確認モーダル(共通Modalコンポーネント) */
export function modal(page: Page, title: string): Locator {
  return page.locator("div.fixed.inset-0").filter({ has: page.getByRole("heading", { name: title }) });
}

/**
 * 指定したAPIへのリクエストを記録する(「送信されないこと」の検証に使う)。
 * 画面はBFF(/api/bff/...)経由でAPIを呼び出すため、BFFのパスをAPIのパス(/api/...)に読み替えて path と照合する。
 */
export function recordApiRequests(page: Page, method: string, path: RegExp): Request[] {
  const requests: Request[] = [];
  page.on("request", (request) => {
    const url = new URL(request.url());
    const apiPath = url.pathname.replace(/^\/api\/bff\//, "/api/");
    if (request.method() === method && url.origin === WEB_URL && url.pathname.startsWith("/api/bff/") && path.test(apiPath)) {
      requests.push(request);
    }
  });
  return requests;
}

/** 画面からのAPI呼び出し(BFF)のURL。例: bffUrl("/projects") → http://localhost:3100/api/bff/projects */
export function bffUrl(apiPath: string): string {
  return `${WEB_URL}/api/bff${apiPath}`;
}

export function tableRow(page: Page, text: string): Locator {
  return page.getByRole("row").filter({ hasText: text });
}

/** 入力欄のブラウザネイティブバリデーションの状態 */
export async function validity(input: Locator): Promise<{ valid: boolean; valueMissing: boolean; typeMismatch: boolean }> {
  return input.evaluate((element) => {
    const { valid, valueMissing, typeMismatch } = (element as HTMLInputElement).validity;
    return { valid, valueMissing, typeMismatch };
  });
}

/**
 * 記録したリクエストが1件も送信されていないことを検証する。
 * 送信処理は非同期に始まるため、少し待ってから確認する。
 */
export async function expectNoRequestSent(page: Page, requests: Request[]): Promise<void> {
  await page.waitForTimeout(500);
  expect(requests, "APIリクエストが送信されていないこと").toHaveLength(0);
}

/** 詳細画面の表示項目(dt/ddの組)の値 */
export function detailField(page: Page, label: string): Locator {
  return page.locator("dt", { hasText: new RegExp(`^${label}$`) }).locator("xpath=following-sibling::dd[1]");
}

/** 画面上のエラーメッセージ(Next.jsが挿入するページ遷移読み上げ用の要素は除く) */
export function alertMessage(page: Page): Locator {
  return page.locator('[role="alert"]:not(#__next-route-announcer__)');
}
