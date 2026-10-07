import { expect, test, unique } from "../../support/fixtures";
import { signIn } from "../../support/ui";

test.describe("7.17 SCR-017 サブタスク・チェックリスト (M2)", () => {
  test("SCR-017-01 タスク詳細でサブタスクを追加できる", async ({ page, data }) => {
    const owner = await data.createUser("オーナー");
    const project = await data.createProject(owner);
    const parent = await data.createTask(project.id, owner, { title: "親タスク" });
    await signIn(page, owner);

    await page.goto(`/tasks/${parent.id}`);

    const form = page.locator("form").filter({ has: page.getByPlaceholder("サブタスクを追加") });
    const name = unique("子");
    await form.getByPlaceholder("サブタスクを追加").fill(name);
    await form.getByRole("button", { name: "追加" }).click();

    await expect(page.getByRole("link", { name: new RegExp(name) })).toBeVisible();
  });

  test("SCR-017-02 タスク詳細でチェックリストを追加して完了にできる", async ({ page, data }) => {
    const owner = await data.createUser("オーナー");
    const project = await data.createProject(owner);
    const task = await data.createTask(project.id, owner);
    await signIn(page, owner);

    await page.goto(`/tasks/${task.id}`);

    const form = page.locator("form").filter({ has: page.getByPlaceholder("項目を追加") });
    await form.getByPlaceholder("項目を追加").fill("レビュー依頼");
    await form.getByRole("button", { name: "追加" }).click();

    const checkbox = page.getByRole("checkbox", { name: "レビュー依頼" });
    await expect(checkbox).not.toBeChecked();
    // 完了トグルは onChange→PATCH→再取得で要素が差し替わるため click で操作し、再取得後の状態を待つ
    await checkbox.click();
    await expect(page.getByRole("checkbox", { name: "レビュー依頼" })).toBeChecked();
  });
});
