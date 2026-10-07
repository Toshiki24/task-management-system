import { expect, test } from "../../support/fixtures";
import { signIn } from "../../support/ui";

test.describe("7.12 SCR-012 カンバンボード (M2)", () => {
  test("SCR-012-01 ボード表示に切り替えると状態列とカードが表示される", async ({ page, data }) => {
    const owner = await data.createUser("オーナー");
    const project = await data.createProject(owner);
    const task = await data.createTask(project.id, owner); // 既定 TODO(未着手)
    await signIn(page, owner);

    await page.goto(`/projects/${project.id}/tasks`);

    // 既定はリスト表示。ボードへ切り替える
    await page.getByRole("tab", { name: "ボード" }).click();

    // 既定ワークフローの3列が出る
    await expect(page.getByRole("region", { name: "列: 未着手" })).toBeVisible();
    await expect(page.getByRole("region", { name: "列: 対応中" })).toBeVisible();
    await expect(page.getByRole("region", { name: "列: 完了" })).toBeVisible();

    // カードは未着手列に表示される
    const todoColumn = page.getByRole("region", { name: "列: 未着手" });
    await expect(todoColumn.getByText(task.title)).toBeVisible();
  });

  test("SCR-012-02 カードをクリックするとタスク詳細へ遷移する", async ({ page, data }) => {
    const owner = await data.createUser("オーナー");
    const project = await data.createProject(owner);
    const task = await data.createTask(project.id, owner);
    await signIn(page, owner);

    await page.goto(`/projects/${project.id}/tasks`);
    await page.getByRole("tab", { name: "ボード" }).click();
    await page.getByRole("region", { name: "列: 未着手" }).getByText(task.title).click();

    await expect(page).toHaveURL(`/tasks/${task.id}`);
  });

  test("SCR-012-03 親カードは件数バッジ、子カードは親名を表示する", async ({ page, data }) => {
    const owner = await data.createUser("オーナー");
    const project = await data.createProject(owner);
    const parent = await data.createTask(project.id, owner, { title: "親タスクA" });
    await data.createTask(project.id, owner, { title: "子タスク1", parentTaskId: parent.id });
    await signIn(page, owner);

    await page.goto(`/projects/${project.id}/tasks`);
    await page.getByRole("tab", { name: "ボード" }).click();

    const todoColumn = page.getByRole("region", { name: "列: 未着手" });
    // 親カード: サブタスク件数バッジ(0/1)が付く
    await expect(todoColumn.getByText("サブタスク 0/1")).toBeVisible();
    // 子カード: どの親のサブタスクかを表示する
    await expect(todoColumn.getByText("↳ 親タスクA のサブタスク")).toBeVisible();
  });
});
