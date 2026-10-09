import { expect, test } from "../../support/fixtures";
import { signIn } from "../../support/ui";

test.describe("7.38 SCR-038 管理コンソール (M5)", () => {
  test("SCR-038-01 System Admin は管理コンソールで統計を見て権限を付与できる", async ({ page, data }) => {
    const admin = await data.createUser("管理者");
    await data.makeSystemAdmin(admin);
    const target = await data.createUser("対象ユーザー");
    await signIn(page, admin);
    await page.goto("/projects");

    // サイドバーの導線から遷移する
    await page.getByRole("link", { name: "管理コンソール" }).click();
    await expect(page).toHaveURL("/admin");
    await expect(page.getByRole("heading", { name: "管理コンソール" })).toBeVisible();

    // 統計カードが表示される
    await expect(page.getByText("システム統計")).toBeVisible();
    await expect(page.getByText("ワークスペース", { exact: true })).toBeVisible();

    // 対象ユーザーに System Admin 権限を付与する
    const row = page.locator(`[data-testid="user-row-${target.id}"]`);
    await expect(row).toContainText("一般");
    await row.getByRole("button", { name: "権限を付与" }).click();
    await expect(row.getByText("System Admin")).toBeVisible();
    await expect(row.getByRole("button", { name: "権限を剥奪" })).toBeVisible();
  });

  test("SCR-038-02 一般ユーザーには導線が出ず、直接アクセスも拒否される", async ({ page, data }) => {
    const user = await data.createUser("一般");
    await signIn(page, user);
    await page.goto("/projects");

    await expect(page.getByRole("link", { name: "管理コンソール" })).toHaveCount(0);

    await page.goto("/admin");
    await expect(page.getByText("この画面には System Admin のみアクセスできます。")).toBeVisible();
  });
});
