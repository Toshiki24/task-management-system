import { bearer, expect, test, unique } from "../../support/fixtures";
import { modal, signIn, tableRow } from "../../support/ui";

test.describe("7.5 SCR-006 タスク一覧画面", () => {
  test("SCR-006-01 一覧表示", async ({ page, data }) => {
    const owner = await data.createUser("オーナー");
    const assignee = await data.createUser("担当者");
    const project = await data.createProject(owner);
    await data.addMember(project.id, owner, assignee);
    const task = await data.createTask(project.id, owner, {
      assigneeId: assignee.id,
      status: "IN_PROGRESS",
      priority: "HIGH",
      dueDate: "2026-11-15",
    });
    await signIn(page, owner);

    await page.goto(`/projects/${project.id}/tasks`);

    await expect(page.getByRole("columnheader")).toHaveText(["タスク名", "担当者", "状態", "優先度", "期限"]);
    await expect(tableRow(page, task.title).getByRole("cell")).toHaveText([
      task.title,
      assignee.name,
      "IN_PROGRESS",
      "HIGH",
      "2026/11/15",
    ]);
  });

  test("SCR-006-02 タスクが0件の場合", async ({ page, data }) => {
    const user = await data.createUser();
    const project = await data.createProject(user);
    await signIn(page, user);

    await page.goto(`/projects/${project.id}/tasks`);

    await expect(page.getByText("タスクがありません。")).toBeVisible();
    await expect(page.getByRole("button", { name: "タスクを追加" })).toBeVisible();
  });

  test("SCR-006-03 タスク追加モーダル表示", async ({ page, data }) => {
    const user = await data.createUser();
    const project = await data.createProject(user);
    await signIn(page, user);
    await page.goto(`/projects/${project.id}/tasks`);

    await page.getByRole("button", { name: "＋ タスク追加" }).click();

    await expect(modal(page, "タスク追加").getByLabel("タイトル *")).toBeVisible();
    await expect(page).toHaveURL(`/projects/${project.id}/tasks`);
  });

  test("SCR-006-04 タスク登録成功", async ({ page, data, api }) => {
    const user = await data.createUser();
    const project = await data.createProject(user);
    await signIn(page, user);
    await page.goto(`/projects/${project.id}/tasks`);
    const title = unique("E2E画面登録タスク");

    await page.getByRole("button", { name: "＋ タスク追加" }).click();
    const dialog = modal(page, "タスク追加");
    await dialog.getByLabel("タイトル *").fill(title);
    await dialog.getByRole("button", { name: "登録" }).click();

    await expect(dialog).toHaveCount(0);
    await expect(tableRow(page, title)).toBeVisible();
    const tasks = await (await api.get(`/api/projects/${project.id}/tasks`, { headers: bearer(user) })).json();
    expect(tasks.map((t: { title: string }) => t.title)).toEqual([title]);
  });

  test("SCR-006-05 担当者選択肢がプロジェクトメンバーに限定される", async ({ page, data }) => {
    const owner = await data.createUser("オーナー");
    const member = await data.createUser("メンバー");
    const outsider = await data.createUser("未所属");
    const project = await data.createProject(owner);
    await data.addMember(project.id, owner, member);
    await signIn(page, owner);
    await page.goto(`/projects/${project.id}/tasks`);

    await page.getByRole("button", { name: "＋ タスク追加" }).click();

    const options = modal(page, "タスク追加").getByLabel("担当者").locator("option");
    await expect(options).toHaveText(["未割り当て", owner.name, member.name]);
    await expect(options.filter({ hasText: outsider.name })).toHaveCount(0);
  });

  test("SCR-006-06 タスク詳細への遷移", async ({ page, data }) => {
    const user = await data.createUser();
    const project = await data.createProject(user);
    const task = await data.createTask(project.id, user);
    await signIn(page, user);
    await page.goto(`/projects/${project.id}/tasks`);

    await tableRow(page, task.title).click();

    await expect(page).toHaveURL(`/tasks/${task.id}`);
    await expect(page.getByRole("heading", { name: "タスク詳細" })).toBeVisible();
  });
});
