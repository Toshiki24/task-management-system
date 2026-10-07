import { expect, test } from "../../support/fixtures";
import { signIn } from "../../support/ui";

test.describe("7.16 SCR-016 My Tasks (M2)", () => {
  test("SCR-016-01 サイドバーから My Tasks を開き、自分の担当が表示される", async ({ page, data }) => {
    const me = await data.createUser("わたし");
    const project = await data.createProject(me);
    const task = await data.createTask(project.id, me, { assigneeId: me.id, title: "自分の担当タスク" });
    await signIn(page, me);
    await page.goto("/projects");

    await page.getByRole("link", { name: "My Tasks" }).click();
    await expect(page).toHaveURL("/me/tasks");

    await expect(page.getByRole("link", { name: /自分の担当タスク/ })).toBeVisible();
    expect(task.id).toBeGreaterThan(0);
  });

  test("SCR-016-02 担当タスクが無ければ案内を表示する", async ({ page, data }) => {
    const me = await data.createUser("わたし");
    await signIn(page, me);

    await page.goto("/me/tasks");
    await expect(page.getByText("担当しているタスクはありません。")).toBeVisible();
  });
});
