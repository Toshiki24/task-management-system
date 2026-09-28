import { WEB_URL } from "../../support/env";
import { expect, test } from "../../support/fixtures";
import { bffUrl, signIn } from "../../support/ui";

const SESSION_COOKIE = "tms_session";

test.describe("7.7 共通レイアウト・認証状態", () => {
  test("SCR-COM-01 ヘッダー表示", async ({ page, data }) => {
    const user = await data.createUser();
    await signIn(page, user);

    await page.goto("/projects");

    const header = page.getByRole("banner");
    await expect(header).toContainText("案件・タスク管理システム");
    await expect(header).toContainText(user.name);
  });

  test("SCR-COM-02 サイドメニュー表示", async ({ page, data }) => {
    const user = await data.createUser();
    await signIn(page, user);

    await page.goto("/projects");

    const nav = page.getByRole("navigation");
    await expect(nav.getByRole("link", { name: "プロジェクト" })).toBeVisible();
    await expect(nav.getByRole("button", { name: "ログアウト" })).toBeVisible();
  });

  test("SCR-COM-03 ログアウト", async ({ page, data }) => {
    const user = await data.createUser();
    await signIn(page, user);
    await page.goto("/projects");

    await page.getByRole("button", { name: "ログアウト" }).click();

    await expect(page).toHaveURL("/login");
    const cookies = await page.context().cookies(WEB_URL);
    expect(cookies.find((c) => c.name === SESSION_COOKIE), "セッションCookieが削除されていること").toBeUndefined();
    // ログアウト後はBFFのセッション確認も認証エラーになる
    expect((await page.request.get("/api/bff/auth/session")).status()).toBe(401);
  });

  test("SCR-COM-04 セッション切れ時の自動リダイレクト", async ({ page, data }) => {
    const user = await data.createUser();
    await signIn(page, user);
    // 改ざん等で復号できないセッションCookieに置き換える
    await page.context().addCookies([{ name: SESSION_COOKIE, value: "invalid-session", url: WEB_URL }]);

    const unauthorized = page.waitForResponse(
      (response) => response.url() === bffUrl("/auth/session") && response.status() === 401,
    );
    await page.goto("/projects");

    await unauthorized;
    await expect(page).toHaveURL("/login");
  });

  test("SCR-COM-05 ローディング表示", async ({ page, data }) => {
    const user = await data.createUser();
    await signIn(page, user);
    // 回線が遅い状況を再現するため、一覧APIの応答を遅らせる
    let release!: () => void;
    const released = new Promise<void>((resolve) => (release = resolve));
    await page.route(bffUrl("/projects"), async (route) => {
      await released;
      await route.continue();
    });

    await page.goto("/projects");

    await expect(page.getByText("データを読み込んでいます...")).toBeVisible();
    release();
    await expect(page.getByText("データを読み込んでいます...")).toHaveCount(0);
  });
});
