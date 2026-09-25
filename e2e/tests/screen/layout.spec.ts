import { API_URL } from "../../support/env";
import { expect, test } from "../../support/fixtures";
import { signIn } from "../../support/ui";

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
    const session = await page.evaluate(() => [localStorage.getItem("accessToken"), localStorage.getItem("currentUser")]);
    expect(session).toEqual([null, null]);
  });

  test("SCR-COM-04 セッション切れ時の自動リダイレクト", async ({ page, data }) => {
    const user = await data.createUser();
    await signIn(page, user);
    await page.evaluate(() => localStorage.setItem("accessToken", "invalid-token"));

    const unauthorized = page.waitForResponse(
      (response) => response.url() === `${API_URL}/api/projects` && response.status() === 401,
    );
    await page.goto("/projects");

    await unauthorized;
    await expect(page).toHaveURL("/login");
    expect(await page.evaluate(() => localStorage.getItem("accessToken"))).toBeNull();
  });

  test("SCR-COM-05 ローディング表示", async ({ page, data }) => {
    const user = await data.createUser();
    await signIn(page, user);
    // 回線が遅い状況を再現するため、一覧APIの応答を遅らせる
    let release!: () => void;
    const released = new Promise<void>((resolve) => (release = resolve));
    await page.route(`${API_URL}/api/projects`, async (route) => {
      await released;
      await route.continue();
    });

    await page.goto("/projects");

    await expect(page.getByText("データを読み込んでいます...")).toBeVisible();
    release();
    await expect(page.getByText("データを読み込んでいます...")).toHaveCount(0);
  });
});
