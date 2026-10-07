import { expect, test } from "../../support/fixtures";
import { signIn } from "../../support/ui";

test.describe("7.18 SCR-018 タスク依存 (M2)", () => {
  test("SCR-018-01 ブロッカーを追加すると一覧に表示され、未完了なら警告が出る", async ({ page, data }) => {
    const owner = await data.createUser("オーナー");
    const project = await data.createProject(owner);
    const taskA = await data.createTask(project.id, owner, { title: "実装タスクA" });
    await data.createTask(project.id, owner, { title: "前提タスクB" });
    await signIn(page, owner);

    await page.goto(`/tasks/${taskA.id}`);

    // 依存関係フォーム(対象タスク select を持つフォーム)を特定して操作する
    const form = page.locator("form").filter({ has: page.getByLabel("対象タスク") });
    await form.getByLabel("対象タスク").selectOption({ label: "前提タスクB" });
    await form.getByRole("button", { name: "追加" }).click();

    // ブロッカーとして表示され、未完了のため警告バナーが出る
    await expect(page.getByRole("link", { name: /前提タスクB/ })).toBeVisible();
    await expect(page.getByText(/未完了のブロッカーが 1 件あります/)).toBeVisible();
  });

  test("SCR-018-02 ブロッカーを削除すると一覧から消える", async ({ page, data }) => {
    const owner = await data.createUser("オーナー");
    const project = await data.createProject(owner);
    const taskA = await data.createTask(project.id, owner, { title: "実装タスクA" });
    await data.createTask(project.id, owner, { title: "前提タスクB" });
    await signIn(page, owner);

    await page.goto(`/tasks/${taskA.id}`);
    const form = page.locator("form").filter({ has: page.getByLabel("対象タスク") });
    await form.getByLabel("対象タスク").selectOption({ label: "前提タスクB" });
    await form.getByRole("button", { name: "追加" }).click();

    await expect(page.getByRole("link", { name: /前提タスクB/ })).toBeVisible();

    await page.getByRole("button", { name: "依存関係を削除" }).click();

    await expect(page.getByRole("link", { name: /前提タスクB/ })).toHaveCount(0);
  });
});
