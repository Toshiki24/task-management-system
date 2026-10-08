import { expect, test, unique } from "../../support/fixtures";
import { signIn } from "../../support/ui";

test.describe("7.20 SCR-020 コマンドパレット (M2)", () => {
  test("SCR-020-01 ヘッダーから開き、タスクを検索して開ける", async ({ page, data }) => {
    const owner = await data.createUser("オーナー");
    const project = await data.createProject(owner);
    const task = await data.createTask(project.id, owner, { title: unique("パレット検索タスク") });
    await signIn(page, owner);
    await page.goto("/projects");

    await page.getByRole("button", { name: "コマンドパレットを開く" }).click();
    const dialog = page.getByRole("dialog", { name: "コマンドパレット" });
    await expect(dialog).toBeVisible();

    await dialog.getByLabel("コマンド・タスクを検索").fill(task.title);
    await dialog.getByRole("button", { name: new RegExp(task.title) }).click();

    await expect(page).toHaveURL(`/tasks/${task.id}`);
  });

  test("SCR-020-02 ナビゲーションコマンドでマイタスクへ移動できる", async ({ page, data }) => {
    const owner = await data.createUser("オーナー");
    await data.createProject(owner);
    await signIn(page, owner);
    await page.goto("/projects");

    await page.getByRole("button", { name: "コマンドパレットを開く" }).click();
    const dialog = page.getByRole("dialog", { name: "コマンドパレット" });
    await dialog.getByLabel("コマンド・タスクを検索").fill("マイ");
    await dialog.getByRole("button", { name: /マイタスク/ }).click();

    await expect(page).toHaveURL("/me/tasks");
  });

  test("SCR-020-03 Ctrl+K で開き、Esc で閉じる", async ({ page, data }) => {
    const owner = await data.createUser("オーナー");
    await signIn(page, owner);
    await page.goto("/projects");
    // レイアウト(コマンドパレット)のマウント完了を待ってからショートカットを押す
    await expect(page.getByRole("button", { name: "コマンドパレットを開く" })).toBeVisible();

    await page.keyboard.press("Control+k");
    await expect(page.getByRole("dialog", { name: "コマンドパレット" })).toBeVisible();

    await page.keyboard.press("Escape");
    await expect(page.getByRole("dialog", { name: "コマンドパレット" })).toHaveCount(0);
  });
});
