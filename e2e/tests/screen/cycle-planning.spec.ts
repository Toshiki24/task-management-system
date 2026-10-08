import { expect, test } from "../../support/fixtures";
import { signIn } from "../../support/ui";

test.describe("7.26 SCR-026 サイクル計画 (M3)", () => {
  test("SCR-026-01 サイクルを作成し、タスクを割り当てられる", async ({ page, data }) => {
    const owner = await data.createUser("オーナー");
    const project = await data.createProject(owner);
    await data.createTask(project.id, owner, { title: "計画対象タスク" });
    await signIn(page, owner);
    await page.goto(`/projects/${project.id}/tasks`);

    // サイクルビューへ切り替え
    await page.getByRole("tab", { name: "サイクル" }).click();

    // サイクルを作成
    await page.getByLabel("サイクルを追加").fill("Sprint 1");
    await page.getByRole("button", { name: "作成" }).click();

    const cycleSection = page.getByRole("region", { name: "サイクル: Sprint 1" });
    await expect(cycleSection).toBeVisible();

    // バックログのタスクを Sprint 1 に割り当てる
    await page.getByLabel("計画対象タスク のサイクル").selectOption({ label: "Sprint 1" });

    // サイクルセクション内にタスクが表示される
    await expect(cycleSection.getByRole("button", { name: /計画対象タスク/ })).toBeVisible();
  });
});
