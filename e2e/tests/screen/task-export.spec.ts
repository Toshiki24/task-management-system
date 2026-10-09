import { expect, test } from "../../support/fixtures";
import { signIn } from "../../support/ui";

test.describe("7.36 SCR-036 CSV エクスポート (M5)", () => {
  test("SCR-036-01 タスク一覧から CSV をダウンロードできる", async ({ page, data }) => {
    const owner = await data.createUser("オーナー");
    const project = await data.createProject(owner);
    await data.createTask(project.id, owner, { title: "出力対象タスク" });
    await signIn(page, owner);
    await page.goto(`/projects/${project.id}/tasks`);

    const [download] = await Promise.all([
      page.waitForEvent("download"),
      page.getByRole("link", { name: "CSV エクスポート" }).click(),
    ]);

    expect(download.suggestedFilename()).toBe(`tasks-${project.id}.csv`);
  });
});
