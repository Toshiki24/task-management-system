import { expect, type Locator, type Page, type Request } from "@playwright/test";
import { API_URL } from "./env";
import type { TestUser } from "./fixtures";

/** ログイン画面を経由せず、ログイン済みの状態(localStorageのセッション)を作る */
export async function signIn(page: Page, user: TestUser): Promise<void> {
  await page.goto("/login");
  await page.evaluate(
    ({ token, currentUser }) => {
      localStorage.setItem("accessToken", token);
      localStorage.setItem("currentUser", JSON.stringify(currentUser));
    },
    { token: user.token, currentUser: { id: user.id, name: user.name, email: user.email } },
  );
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

/** 指定したAPIへのリクエストを記録する(「送信されないこと」の検証に使う) */
export function recordApiRequests(page: Page, method: string, path: RegExp): Request[] {
  const requests: Request[] = [];
  page.on("request", (request) => {
    const url = request.url();
    if (request.method() === method && url.startsWith(API_URL) && path.test(new URL(url).pathname)) {
      requests.push(request);
    }
  });
  return requests;
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
