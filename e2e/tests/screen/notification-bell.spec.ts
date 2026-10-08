import { expect, test } from "../../support/fixtures";
import { signIn } from "../../support/ui";

test.describe("7.25 SCR-025 通知ベル (M3)", () => {
  test("SCR-025-01 担当通知がベルに出て、開いて選ぶとタスクへ遷移する", async ({ page, data }) => {
    const owner = await data.createUser("オーナー");
    const member = await data.createUser("担当");
    const project = await data.createProject(owner);
    await data.addMember(project.id, owner, member);
    // owner が member を担当にする → member へ ASSIGNED 通知
    const task = await data.createTask(project.id, owner, { assigneeId: member.id, title: "通知対象タスク" });

    await signIn(page, member);
    await page.goto("/projects");

    const bell = page.getByRole("button", { name: "通知" });
    // 未読バッジが出る
    await expect(bell.getByText("1")).toBeVisible();

    await bell.click();
    // ドロップダウンに担当通知が並ぶ
    await expect(page.getByText(/があなたを担当に設定しました/)).toBeVisible();
    await page.getByText(/があなたを担当に設定しました/).click();

    await expect(page).toHaveURL(`/tasks/${task.id}`);
  });

  test("SCR-025-02 すべて既読で未読バッジが消える", async ({ page, data }) => {
    const owner = await data.createUser("オーナー");
    const member = await data.createUser("担当");
    const project = await data.createProject(owner);
    await data.addMember(project.id, owner, member);
    await data.createTask(project.id, owner, { assigneeId: member.id });

    await signIn(page, member);
    await page.goto("/projects");

    const bell = page.getByRole("button", { name: "通知" });
    await expect(bell.getByText("1")).toBeVisible();

    await bell.click();
    await page.getByRole("button", { name: "すべて既読" }).click();

    await expect(bell.getByText("1")).toHaveCount(0);
  });
});
