import { expect, test } from "../../support/fixtures";
import { signIn } from "../../support/ui";

test.describe("7.27 SCR-027 マイルストーン計画 (M3)", () => {
  test("SCR-027-01 マイルストーンを作成し、タスクを割り当てられる", async ({ page, data }) => {
    const owner = await data.createUser("オーナー");
    const project = await data.createProject(owner);
    await data.createTask(project.id, owner, { title: "リリース対象タスク" });
    await signIn(page, owner);
    await page.goto(`/projects/${project.id}/tasks`);

    // マイルストーンビューへ切り替え
    await page.getByRole("tab", { name: "マイルストーン" }).click();

    // マイルストーンを作成
    await page.getByLabel("マイルストーンを追加").fill("v1.0");
    await page.getByRole("button", { name: "作成" }).click();

    const section = page.getByRole("region", { name: "マイルストーン: v1.0" });
    await expect(section).toBeVisible();

    // 未割り当てのタスクを v1.0 に割り当てる
    await page.getByLabel("リリース対象タスク のマイルストーン").selectOption({ label: "v1.0" });

    // マイルストーンセクション内にタスクが表示される
    await expect(section.getByRole("button", { name: /リリース対象タスク/ })).toBeVisible();
  });
});
