import { bearer, expect, test } from "../../support/fixtures";
import { signIn, tableRow } from "../../support/ui";

test.describe("7.19 SCR-019 一括操作 (M2)", () => {
  test("SCR-019-01 一括操作モードで複数選択し、状態をまとめて変更できる", async ({ page, data, api }) => {
    const owner = await data.createUser("オーナー");
    const project = await data.createProject(owner);
    const t1 = await data.createTask(project.id, owner, { title: "一括タスク1", status: "TODO" });
    const t2 = await data.createTask(project.id, owner, { title: "一括タスク2", status: "TODO" });
    await signIn(page, owner);
    await page.goto(`/projects/${project.id}/tasks`);

    // 一括操作モードに入る
    await page.getByRole("button", { name: "一括操作" }).click();

    // 2件を選択
    await tableRow(page, "一括タスク1").getByRole("checkbox").check();
    await tableRow(page, "一括タスク2").getByRole("checkbox").check();
    await expect(page.getByText("2 件選択中")).toBeVisible();

    // 状態を「完了」にして適用
    await page.getByLabel("状態").selectOption({ label: "完了" });
    await page.getByRole("button", { name: "適用" }).click();

    // 両方が DONE になる
    await expect
      .poll(async () => {
        const tasks = await (await api.get(`/api/projects/${project.id}/tasks`, { headers: bearer(owner) })).json();
        return tasks.every((t: { status: string }) => t.status === "DONE");
      })
      .toBe(true);
    expect(t1.id).toBeGreaterThan(0);
    expect(t2.id).toBeGreaterThan(0);
  });

  test("SCR-019-02 全選択チェックで全件選択できる", async ({ page, data }) => {
    const owner = await data.createUser("オーナー");
    const project = await data.createProject(owner);
    await data.createTask(project.id, owner, { title: "タスクA" });
    await data.createTask(project.id, owner, { title: "タスクB" });
    await signIn(page, owner);
    await page.goto(`/projects/${project.id}/tasks`);

    await page.getByRole("button", { name: "一括操作" }).click();
    await page.getByRole("checkbox", { name: "全て選択" }).check();

    await expect(page.getByText("2 件選択中")).toBeVisible();
  });
});
