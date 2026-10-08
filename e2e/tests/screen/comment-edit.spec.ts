import { expect, test } from "../../support/fixtures";
import { signIn } from "../../support/ui";

function commentsSection(page: import("@playwright/test").Page) {
  return page.locator("div").filter({ has: page.getByRole("heading", { name: "コメント" }) }).last();
}

test.describe("7.22 SCR-022 コメント拡張 (M3)", () => {
  test("SCR-022-01 コメントが Markdown で描画される", async ({ page, data }) => {
    const user = await data.createUser();
    const project = await data.createProject(user);
    const task = await data.createTask(project.id, user);
    await data.createComment(task.id, user, "**強調**テキスト");
    await signIn(page, user);

    await page.goto(`/tasks/${task.id}`);

    await expect(commentsSection(page).locator("strong")).toHaveText("強調");
  });

  test("SCR-022-02 自分のコメントを編集できる", async ({ page, data }) => {
    const user = await data.createUser();
    const project = await data.createProject(user);
    const task = await data.createTask(project.id, user);
    await data.createComment(task.id, user, "編集前の本文");
    await signIn(page, user);
    await page.goto(`/tasks/${task.id}`);

    const section = commentsSection(page);
    await section.getByRole("button", { name: "編集" }).click();
    await section.getByLabel("コメントを編集").fill("編集後の本文");
    await section.getByRole("button", { name: "更新" }).click();

    await expect(section.getByText("編集後の本文")).toBeVisible();
    await expect(section.getByText("(編集済み)")).toBeVisible();
  });

  test("SCR-022-03 自分のコメントを削除すると本文が伏せられる", async ({ page, data }) => {
    const user = await data.createUser();
    const project = await data.createProject(user);
    const task = await data.createTask(project.id, user);
    await data.createComment(task.id, user, "消えるコメント本文");
    await signIn(page, user);
    await page.goto(`/tasks/${task.id}`);

    const section = commentsSection(page);
    await section.getByRole("button", { name: "コメントを削除" }).click();

    await expect(section.getByText("削除されたコメント")).toBeVisible();
    await expect(section.getByText("消えるコメント本文")).toHaveCount(0);
  });
});
