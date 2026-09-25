import { bearer, expect, test, unique } from "../../support/fixtures";
import { alertMessage, detailField, expectNoRequestSent, modal, recordApiRequests, signIn, tableRow } from "../../support/ui";

test.describe("7.6 SCR-007 タスク詳細画面", () => {
  test("SCR-007-01 詳細表示", async ({ page, data }) => {
    const owner = await data.createUser("オーナー");
    const assignee = await data.createUser("担当者");
    const project = await data.createProject(owner);
    await data.addMember(project.id, owner, assignee);
    const task = await data.createTask(project.id, owner, {
      assigneeId: assignee.id,
      description: "詳細表示確認用の説明",
      status: "DONE",
      priority: "LOW",
      dueDate: "2026-11-15",
    });
    await signIn(page, owner);

    await page.goto(`/tasks/${task.id}`);

    await expect(detailField(page, "タイトル")).toHaveText(task.title);
    await expect(detailField(page, "ステータス")).toHaveText("DONE");
    await expect(detailField(page, "優先度")).toHaveText("LOW");
    await expect(detailField(page, "担当者")).toHaveText(assignee.name);
    await expect(detailField(page, "期限")).toHaveText("2026/11/15");
    await expect(detailField(page, "説明")).toHaveText("詳細表示確認用の説明");
  });

  test("SCR-007-02 編集モードへの切り替え・保存", async ({ page, data }) => {
    const owner = await data.createUser("オーナー");
    const assignee = await data.createUser("担当者");
    const project = await data.createProject(owner);
    await data.addMember(project.id, owner, assignee);
    const task = await data.createTask(project.id, owner);
    await signIn(page, owner);
    await page.goto(`/tasks/${task.id}`);
    const newTitle = unique("E2E編集後タスク");

    await page.getByRole("button", { name: "編集" }).click();
    await expect(page.getByLabel("タイトル *")).toHaveValue(task.title);
    await page.getByLabel("タイトル *").fill(newTitle);
    await page.getByLabel("担当者").selectOption({ label: assignee.name });
    await page.getByLabel("ステータス").selectOption("IN_PROGRESS");
    await page.getByLabel("優先度").selectOption("HIGH");
    await page.getByLabel("期限").fill("2026-12-24");
    await page.getByRole("button", { name: "保存" }).click();

    await expect(page.getByRole("button", { name: "編集" })).toBeVisible();
    await expect(detailField(page, "タイトル")).toHaveText(newTitle);
    await expect(detailField(page, "担当者")).toHaveText(assignee.name);
    await expect(detailField(page, "ステータス")).toHaveText("IN_PROGRESS");
    await expect(detailField(page, "優先度")).toHaveText("HIGH");
    await expect(detailField(page, "期限")).toHaveText("2026/12/24");
  });

  test("SCR-007-03 削除確認モーダル・実行", async ({ page, data, api }) => {
    const user = await data.createUser();
    const project = await data.createProject(user);
    const task = await data.createTask(project.id, user);
    await signIn(page, user);
    await page.goto(`/tasks/${task.id}`);

    await page.getByRole("button", { name: "削除" }).click();
    const dialog = modal(page, "タスク削除");
    await expect(dialog).toContainText("このタスクを削除しますか？");
    await dialog.getByRole("button", { name: "削除" }).click();

    await expect(page).toHaveURL(`/projects/${project.id}/tasks`);
    await expect(page.getByText("タスクがありません。")).toBeVisible();
    await expect(tableRow(page, task.title)).toHaveCount(0);
    expect((await api.get(`/api/tasks/${task.id}`, { headers: bearer(user) })).status()).toBe(404);
  });

  test("SCR-007-04 コメント一覧表示", async ({ page, data }) => {
    const user = await data.createUser();
    const project = await data.createProject(user);
    const task = await data.createTask(project.id, user);
    const comment = await data.createComment(task.id, user);
    await signIn(page, user);

    await page.goto(`/tasks/${task.id}`);

    const item = page.getByRole("listitem").filter({ hasText: comment.comment });
    await expect(item.locator("p")).toHaveText([user.name, comment.comment, /^\d{4}\/\d{2}\/\d{2} \d{2}:\d{2}$/]);
  });

  test("SCR-007-05 コメントが0件の場合", async ({ page, data }) => {
    const user = await data.createUser();
    const project = await data.createProject(user);
    const task = await data.createTask(project.id, user);
    await signIn(page, user);

    await page.goto(`/tasks/${task.id}`);

    await expect(page.getByText("コメントはまだありません。")).toBeVisible();
  });

  test("SCR-007-06 コメント投稿成功", async ({ page, data }) => {
    const user = await data.createUser();
    const project = await data.createProject(user);
    const task = await data.createTask(project.id, user);
    await signIn(page, user);
    await page.goto(`/tasks/${task.id}`);
    const text = unique("E2E画面投稿コメント");

    await page.getByPlaceholder("コメントを入力してください").fill(text);
    await page.getByRole("button", { name: "投稿" }).click();

    await expect(page.getByRole("listitem").filter({ hasText: text })).toContainText(user.name);
    await expect(page.getByPlaceholder("コメントを入力してください")).toHaveValue("");
  });

  test("SCR-007-07 空白のみのコメント投稿", async ({ page, data }) => {
    const user = await data.createUser();
    const project = await data.createProject(user);
    const task = await data.createTask(project.id, user);
    await signIn(page, user);
    await page.goto(`/tasks/${task.id}`);
    const postRequests = recordApiRequests(page, "POST", /^\/api\/tasks\/\d+\/comments$/);

    await page.getByPlaceholder("コメントを入力してください").fill("   \n  ");
    await page.getByRole("button", { name: "投稿" }).click();

    await expect(alertMessage(page)).toHaveText("コメントを入力してください。");
    await expectNoRequestSent(page, postRequests);
  });
});
