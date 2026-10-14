import { expect, test } from "../../support/fixtures";

test.describe("7.40 SCR-040 初回セットアップ (M5 自己ホスト)", () => {
  test("SCR-040-01 セットアップ済みなら /setup は通常ログインへ誘導される", async ({ page, data }) => {
    // 共有DBには既にユーザーが存在する(= セットアップ済み相当)
    await data.createUser("既存");

    await page.goto("/setup");

    // サーバーの状態判定に従い、ログイン画面へリダイレクトされる
    await expect(page).toHaveURL("/login");
    await expect(page.getByRole("button", { name: "ログイン" })).toBeVisible();
  });
});
