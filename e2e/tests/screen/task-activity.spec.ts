import { expect, test } from "../../support/fixtures";
import { signIn } from "../../support/ui";

test.describe("7.21 SCR-021 アクティビティ (M3)", () => {
  test("SCR-021-01 タスク詳細に作成アクティビティが表示される", async ({ page, data }) => {
    const owner = await data.createUser("オーナー");
    const project = await data.createProject(owner);
    const task = await data.createTask(project.id, owner, { title: "活動タスク" });
    await signIn(page, owner);

    await page.goto(`/tasks/${task.id}`);

    const section = page.locator("div").filter({ has: page.getByRole("heading", { name: "アクティビティ" }) }).last();
    await expect(section.getByText(/タスクを作成しました/)).toBeVisible();
  });

  test("SCR-021-02 編集するとアクティビティに変更が追記される", async ({ page, data }) => {
    const owner = await data.createUser("オーナー");
    const project = await data.createProject(owner);
    const task = await data.createTask(project.id, owner, { title: "編集前タスク" });
    await signIn(page, owner);
    await page.goto(`/tasks/${task.id}`);

    await page.getByRole("button", { name: "編集" }).click();
    await page.getByLabel("タイトル *").fill("編集後タスク");
    await page.getByRole("button", { name: "保存" }).click();

    await expect(page.getByText(/を変更しました/)).toBeVisible();
  });
});
