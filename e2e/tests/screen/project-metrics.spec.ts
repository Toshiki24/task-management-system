import { expect, test } from "../../support/fixtures";
import { signIn } from "../../support/ui";

test.describe("7.35 SCR-035 ダッシュボード (M5)", () => {
  test("SCR-035-01 プロジェクトの指標が表示される", async ({ page, data }) => {
    const owner = await data.createUser("オーナー");
    const project = await data.createProject(owner);
    await data.createTask(project.id, owner, { status: "TODO" });
    await data.createTask(project.id, owner, { status: "DONE" });

    await signIn(page, owner);
    await page.goto(`/projects/${project.id}`);
    await page.getByRole("link", { name: "指標・ダッシュボードを見る" }).click();

    await expect(page.getByRole("heading", { name: "ダッシュボード" })).toBeVisible();
    // 完了率 50%(2件中1件完了)
    await expect(page.getByText("50%")).toBeVisible();
    await expect(page.getByRole("heading", { name: "状態別の件数" })).toBeVisible();
    await expect(page.getByRole("heading", { name: "担当別の負荷" })).toBeVisible();
    // 開発指標セクション(直近30日)
    await expect(page.getByRole("heading", { name: "開発指標（直近30日）" })).toBeVisible();
  });
});
