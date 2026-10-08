import { expect, test } from "../../support/fixtures";
import { signIn } from "../../support/ui";

test.describe("7.24 SCR-024 ウォッチャー (M3)", () => {
  test("SCR-024-01 フォロー/解除ができ、一覧に自分が表示される", async ({ page, data }) => {
    const owner = await data.createUser("オーナー");
    const project = await data.createProject(owner);
    const task = await data.createTask(project.id, owner);
    await signIn(page, owner);

    await page.goto(`/tasks/${task.id}`);

    await page.getByRole("button", { name: "フォロー", exact: true }).click();

    await expect(page.getByRole("button", { name: "フォロー中" })).toBeVisible();
    // ウォッチャー一覧のチップ(li のテキストが名前と完全一致)に自分が表示される
    // (アクティビティ等にも名前が出るため、完全一致で絞る)
    const exactName = new RegExp(`^${owner.name.replace(/[.*+?^${}()|[\]\\]/g, "\\$&")}$`);
    await expect(page.getByRole("listitem").filter({ hasText: exactName })).toBeVisible();

    await page.getByRole("button", { name: "フォロー中" }).click();
    await expect(page.getByRole("button", { name: "フォロー", exact: true })).toBeVisible();
  });
});
