import { expect, test } from "../../support/fixtures";
import { signIn } from "../../support/ui";

test.describe("7.28 SCR-028 リアルタイム更新 (M3)", () => {
  test("SCR-028-01 他者が追加したタスクがフォーカス時に一覧へ反映される", async ({ page, data }) => {
    const owner = await data.createUser("オーナー");
    const project = await data.createProject(owner);
    await data.createTask(project.id, owner, { title: "既存タスク" });
    await signIn(page, owner);
    await page.goto(`/projects/${project.id}/tasks`);

    await expect(page.getByText("既存タスク")).toBeVisible();

    // 別クライアント(他者)による追加をシミュレートする
    await data.createTask(project.id, owner, { title: "あとから追加タスク" });

    // フォーカスを当てると最新を再取得し、追加タスクが反映される(M3 §7)
    await page.evaluate(() => window.dispatchEvent(new Event("focus")));
    await expect(page.getByText("あとから追加タスク")).toBeVisible();
  });
});
