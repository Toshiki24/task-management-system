import type { Page } from "@playwright/test";
import { API_URL, WEB_URL } from "../../support/env";
import { expect, test, unique } from "../../support/fixtures";
import { BFF_HEADERS, loginViaUi, signIn, tableRow } from "../../support/ui";

const SESSION_COOKIE = "tms_session";
const CSRF_REJECTED = { message: "不正なリクエストです。" };
const EVIL_ORIGIN = "https://evil.example.test";

/**
 * アクセストークンが再発行されるまでの待ち時間。
 * E2Eでは有効期限を12秒にしており(scripts/start-backend.mjs)、BFFは期限の10秒前から再発行するため、
 * ログインから2秒を過ぎた呼び出しで再発行が行われる。
 */
const UNTIL_REFRESH_MS = 3_000;

async function sessionCookie(page: Page) {
  return (await page.context().cookies(WEB_URL)).find((c) => c.name === SESSION_COOKIE);
}

function newProjectBody() {
  return { name: unique("E2E-CSRF確認"), status: "ACTIVE" };
}

test.describe("9.8 SEC-04 トークンの保管（BFF）", () => {
  test("SEC-04-01 ログイン後、ブラウザのJavaScriptからトークンを読み取れない", async ({ page, data }) => {
    const user = await data.createUser();
    await loginViaUi(page, user);

    const storage = await page.evaluate(() => ({
      localStorage: JSON.stringify({ ...localStorage }),
      sessionStorage: JSON.stringify({ ...sessionStorage }),
      cookie: document.cookie,
    }));

    // JWTは "eyJ"(Base64の '{"')で始まる
    expect(storage.localStorage).not.toContain("eyJ");
    expect(storage.sessionStorage).not.toContain("eyJ");
    expect(storage.cookie, "HttpOnlyのセッションCookieはdocument.cookieに現れない").not.toContain(SESSION_COOKIE);
  });

  test("SEC-04-02 セッションCookieに HttpOnly・Secure・SameSite=Lax が設定される", async ({ page, data }) => {
    const user = await data.createUser();
    await loginViaUi(page, user);

    const cookie = await sessionCookie(page);

    expect(cookie).toBeDefined();
    expect(cookie!.httpOnly).toBe(true);
    // E2Eは本番ビルド(next start)で実行しているため、本番と同じく Secure が付く
    expect(cookie!.secure).toBe(true);
    expect(cookie!.sameSite).toBe("Lax");
  });

  test("SEC-04-03 セッションCookieは暗号化されており、トークンやユーザー情報を平文で含まない", async ({ page, data }) => {
    const user = await data.createUser();
    await loginViaUi(page, user);

    const cookie = await sessionCookie(page);

    expect(cookie!.value).not.toContain("eyJ");
    expect(decodeURIComponent(cookie!.value)).not.toContain(user.email);
  });

  test("SEC-04-04 画面操作中、ブラウザからAPIサーバーへ直接通信しない", async ({ page, data }) => {
    const user = await data.createUser();
    const project = await data.createProject(user);
    await data.createTask(project.id, user);
    const apiRequests: string[] = [];
    page.on("request", (request) => {
      if (request.url().startsWith(API_URL)) apiRequests.push(request.url());
    });

    await loginViaUi(page, user);
    await tableRow(page, project.name).click();
    await expect(page.getByRole("heading", { name: "プロジェクト詳細" })).toBeVisible();
    await page.goto(`/projects/${project.id}/tasks`);
    await expect(page.getByRole("heading", { name: "タスク一覧" })).toBeVisible();

    expect(apiRequests).toEqual([]);
  });

  test("SEC-04-05 アクセストークンの期限が近づくと自動で再発行され、操作を続けられる", async ({ page, data, db }) => {
    const user = await data.createUser();
    const project = await data.createProject(user);
    await loginViaUi(page, user);

    await page.waitForTimeout(UNTIL_REFRESH_MS);
    await page.goto(`/projects/${project.id}`);

    await expect(page.getByRole("heading", { name: "プロジェクト詳細" })).toBeVisible();
    await expect(page).toHaveURL(`/projects/${project.id}`);
    const { rows } = await db.query<{ count: string }>(
      "SELECT count(*) FROM refresh_tokens WHERE user_id = $1 AND revoked_reason = 'ROTATED'",
      [user.id],
    );
    expect(Number(rows[0].count), "リフレッシュトークンによる再発行(置き換え)が行われていること").toBeGreaterThan(0);
  });

  test("SEC-04-06 ログアウトすると、リフレッシュトークンがAPI側で失効する", async ({ page, data, db }) => {
    const user = await data.createUser();
    // テストデータ作成時のAPIログインで発行されたトークンと区別するため、画面からのログイン前の最大IDを控える
    const before = await db.query<{ max_id: string | null }>(
      "SELECT max(id) AS max_id FROM refresh_tokens WHERE user_id = $1",
      [user.id],
    );
    const baselineId = Number(before.rows[0].max_id ?? 0);
    await loginViaUi(page, user);

    await page.getByRole("button", { name: "ログアウト" }).click();
    await expect(page).toHaveURL("/login");

    // 画面からのログイン以降に発行されたトークン(再発行されたものを含む)が、すべて失効していること
    const { rows } = await db.query<{ active: string; logout: string }>(
      `SELECT count(*) FILTER (WHERE revoked_at IS NULL) AS active,
              count(*) FILTER (WHERE revoked_reason = 'LOGOUT') AS logout
         FROM refresh_tokens WHERE user_id = $1 AND id > $2`,
      [user.id, baselineId],
    );
    expect(Number(rows[0].active), "有効なリフレッシュトークンが残っていないこと").toBe(0);
    expect(Number(rows[0].logout)).toBeGreaterThan(0);
  });

  test("SEC-04-07 リフレッシュトークンがAPI側で失効すると、画面のセッションも無効になる", async ({ page, data, db }) => {
    const user = await data.createUser();
    await loginViaUi(page, user);
    // 別の端末からのログアウトや再利用検知などで、API側のトークンが失効した状態を作る
    await db.query(
      "UPDATE refresh_tokens SET revoked_at = now(), revoked_reason = 'LOGOUT' WHERE user_id = $1",
      [user.id],
    );

    await page.waitForTimeout(UNTIL_REFRESH_MS);
    await page.goto("/projects");

    await expect(page).toHaveURL("/login");
  });

  test("SEC-04-08 BFFはリフレッシュAPIを中継しない", async ({ page, data }) => {
    const user = await data.createUser();
    await signIn(page, user);

    const response = await page.request.post("/api/bff/auth/refresh", {
      headers: BFF_HEADERS,
      data: { refreshToken: "dummy" },
    });

    expect(response.status()).toBe(404);
  });
});

test.describe("9.9 CSRF対策（BFF）", () => {
  test("SEC-04-09 他サイトのOriginからの更新リクエストは拒否される", async ({ page, data, db }) => {
    const user = await data.createUser();
    await signIn(page, user);
    const body = newProjectBody();

    const response = await page.request.post("/api/bff/projects", {
      headers: { ...BFF_HEADERS, Origin: EVIL_ORIGIN },
      data: body,
    });

    expect(response.status()).toBe(403);
    expect(await response.json()).toEqual(CSRF_REJECTED);
    const { rowCount } = await db.query("SELECT 1 FROM projects WHERE name = $1", [body.name]);
    expect(rowCount).toBe(0);
  });

  test("SEC-04-10 独自ヘッダー（X-Requested-With）のない更新リクエストは拒否される", async ({ page, data, db }) => {
    const user = await data.createUser();
    await signIn(page, user);
    const body = newProjectBody();

    const response = await page.request.post("/api/bff/projects", { headers: { Origin: WEB_URL }, data: body });

    expect(response.status()).toBe(403);
    expect(await response.json()).toEqual(CSRF_REJECTED);
    const { rowCount } = await db.query("SELECT 1 FROM projects WHERE name = $1", [body.name]);
    expect(rowCount).toBe(0);
  });

  test("SEC-04-11 Originヘッダーのない更新リクエストは拒否される", async ({ page, data, db }) => {
    const user = await data.createUser();
    await signIn(page, user);
    const body = newProjectBody();

    const response = await page.request.post("/api/bff/projects", {
      headers: { "X-Requested-With": "XMLHttpRequest" },
      data: body,
    });

    expect(response.status()).toBe(403);
    expect(await response.json()).toEqual(CSRF_REJECTED);
    const { rowCount } = await db.query("SELECT 1 FROM projects WHERE name = $1", [body.name]);
    expect(rowCount).toBe(0);
  });

  test("SEC-04-12 他サイトからのログインリクエストは拒否される（ログインCSRF対策）", async ({ page, data }) => {
    const user = await data.createUser();

    const response = await page.request.post("/api/bff/auth/login", {
      headers: { ...BFF_HEADERS, Origin: EVIL_ORIGIN },
      data: { email: user.email, password: user.password },
    });

    expect(response.status()).toBe(403);
    expect(await sessionCookie(page), "セッションCookieが発行されないこと").toBeUndefined();
  });

  test("SEC-04-13 自サイトの画面からの更新リクエストは受け付けられる", async ({ page, data }) => {
    const user = await data.createUser();
    await signIn(page, user);
    const body = newProjectBody();

    const response = await page.request.post("/api/bff/projects", { headers: BFF_HEADERS, data: body });

    expect(response.status()).toBe(201);
    data.trackProject((await response.json()).id);
  });
});
