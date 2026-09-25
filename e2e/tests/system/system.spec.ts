import type { Page } from "@playwright/test";
import { bearer, expect, test, unique, type TestDataFactory } from "../../support/fixtures";
import { alertMessage, detailField, loginViaUi, modal, signIn, tableRow } from "../../support/ui";

async function createProjectViaUi(page: Page, data: TestDataFactory, name: string): Promise<number> {
  await page.getByRole("link", { name: "＋ 新規作成" }).click();
  await page.getByLabel("プロジェクト名 *").fill(name);
  await page.getByLabel("説明").fill("システムテストで作成したプロジェクト");
  await page.getByLabel("開始日").fill("2026-10-01");
  await page.getByLabel("終了日").fill("2026-12-31");
  await page.getByRole("button", { name: "登録" }).click();
  await expect(page).toHaveURL(/\/projects\/\d+$/);
  const projectId = Number(new URL(page.url()).pathname.split("/").pop());
  data.trackProject(projectId);
  return projectId;
}

async function addMemberViaUi(page: Page, member: { name: string; email: string }): Promise<void> {
  await page.getByRole("button", { name: "メンバー追加" }).click();
  await page.getByLabel("ユーザー").selectOption({ label: `${member.name} (${member.email})` });
  await page.getByLabel("ロール").selectOption("MEMBER");
  await page.getByRole("button", { name: "追加", exact: true }).click();
  await expect(page.getByRole("listitem").filter({ hasText: member.name })).toContainText(
    `${member.name} (${member.email} / MEMBER)`,
  );
}

async function addTaskViaUi(page: Page, title: string): Promise<void> {
  await page.getByRole("button", { name: "＋ タスク追加" }).click();
  const dialog = modal(page, "タスク追加");
  await dialog.getByLabel("タイトル *").fill(title);
  await dialog.getByRole("button", { name: "登録" }).click();
  await expect(dialog).toHaveCount(0);
  await expect(tableRow(page, title)).toBeVisible();
}

async function postCommentViaUi(page: Page, text: string, authorName: string): Promise<void> {
  await page.getByPlaceholder("コメントを入力してください").fill(text);
  await page.getByRole("button", { name: "投稿" }).click();
  await expect(page.getByRole("listitem").filter({ hasText: text })).toContainText(authorName);
}

test.describe("8. システムテスト", () => {
  test("ST-001 一連のメインフロー", async ({ page, data }) => {
    const user = await data.createUser("担当者A");
    const member = await data.createUser("メンバーB");
    const pageErrors: Error[] = [];
    page.on("pageerror", (error) => pageErrors.push(error));

    // ① ログイン
    await loginViaUi(page, user);
    await expect(page.getByRole("banner")).toContainText(user.name);

    // ② プロジェクト新規作成
    const projectName = unique("E2Eメインフロー");
    const projectId = await createProjectViaUi(page, data, projectName);
    await expect(detailField(page, "プロジェクト名")).toHaveText(projectName);

    // ③ プロジェクト詳細でメンバー追加
    await addMemberViaUi(page, member);

    // ④ タスク一覧でタスク追加
    await page.getByRole("link", { name: "タスク一覧を見る" }).click();
    await expect(page).toHaveURL(`/projects/${projectId}/tasks`);
    const taskTitle = unique("E2Eメインフロータスク");
    await addTaskViaUi(page, taskTitle);

    // ⑤ タスク詳細で担当者・ステータス・優先度・期限を編集
    await tableRow(page, taskTitle).click();
    await expect(page).toHaveURL(/\/tasks\/\d+$/);
    await page.getByRole("button", { name: "編集" }).click();
    await page.getByLabel("担当者").selectOption({ label: member.name });
    await page.getByLabel("ステータス").selectOption("IN_PROGRESS");
    await page.getByLabel("優先度").selectOption("HIGH");
    await page.getByLabel("期限").fill("2026-11-30");
    await page.getByRole("button", { name: "保存" }).click();
    await expect(detailField(page, "担当者")).toHaveText(member.name);
    await expect(detailField(page, "ステータス")).toHaveText("IN_PROGRESS");
    await expect(detailField(page, "優先度")).toHaveText("HIGH");
    await expect(detailField(page, "期限")).toHaveText("2026/11/30");

    // ⑥ コメント投稿
    await postCommentViaUi(page, unique("E2Eメインフローコメント"), user.name);

    // ⑦ ログアウト
    await page.getByRole("button", { name: "ログアウト" }).click();
    await expect(page).toHaveURL("/login");

    await expect(alertMessage(page)).toHaveCount(0);
    expect(pageErrors).toEqual([]);
  });

  test("ST-002 プロジェクト削除に伴う関連データの整合性", async ({ page, data, api }) => {
    const owner = await data.createUser("オーナー");
    const member = await data.createUser("メンバー");
    const project = await data.createProject(owner);
    await data.addMember(project.id, owner, member);
    const task = await data.createTask(project.id, owner, { assigneeId: member.id });
    await data.createComment(task.id, member);
    await signIn(page, owner);
    await page.goto(`/projects/${project.id}`);

    await page.getByRole("button", { name: "削除", exact: true }).first().click();
    await modal(page, "プロジェクト削除").getByRole("button", { name: "削除" }).click();
    await expect(page).toHaveURL("/projects");

    expect((await api.get(`/api/tasks/${task.id}`, { headers: bearer(owner) })).status()).toBe(404);
    await page.goto(`/tasks/${task.id}`);
    await expect(alertMessage(page)).toHaveText("指定されたタスクが存在しません。");
  });

  test("ST-003 複数ユーザーでの共同作業", async ({ browser, data, api }) => {
    const userA = await data.createUser("ユーザーA");
    const userB = await data.createUser("ユーザーB");
    const contextA = await browser.newContext();
    const contextB = await browser.newContext();
    try {
      // ユーザーA: プロジェクトを作成し、ユーザーBをメンバーに追加する
      const pageA = await contextA.newPage();
      await loginViaUi(pageA, userA);
      const projectId = await createProjectViaUi(pageA, data, unique("E2E共同作業"));
      await addMemberViaUi(pageA, userB);

      // ユーザーB: 招待されたプロジェクトでタスクを作成し、コメントを投稿する
      const pageB = await contextB.newPage();
      await loginViaUi(pageB, userB);
      await pageB.goto(`/projects/${projectId}/tasks`);
      const taskTitle = unique("E2E共同作業タスク");
      await addTaskViaUi(pageB, taskTitle);
      await tableRow(pageB, taskTitle).click();
      await expect(pageB).toHaveURL(/\/tasks\/\d+$/);
      const taskId = Number(new URL(pageB.url()).pathname.split("/").pop());
      const commentText = unique("E2EユーザーBのコメント");
      await postCommentViaUi(pageB, commentText, userB.name);

      // ユーザーAの画面からも、投稿者がユーザーBとして記録されていることを確認する
      await pageA.goto(`/tasks/${taskId}`);
      await expect(pageA.getByRole("listitem").filter({ hasText: commentText })).toContainText(userB.name);
      const comments = await (await api.get(`/api/tasks/${taskId}/comments`, { headers: bearer(userA) })).json();
      expect(comments).toEqual([expect.objectContaining({ comment: commentText, userId: userB.id, userName: userB.name })]);
    } finally {
      await contextA.close();
      await contextB.close();
    }
  });

  test("ST-004 ブラウザ再読み込み後のセッション維持", async ({ page, data }) => {
    const user = await data.createUser();
    const project = await data.createProject(user);
    await loginViaUi(page, user);
    await page.goto(`/projects/${project.id}`);

    await page.reload();

    await expect(page).toHaveURL(`/projects/${project.id}`);
    await expect(page.getByRole("banner")).toContainText(user.name);
    await expect(detailField(page, "プロジェクト名")).toHaveText(project.name);
  });

  test("ST-005 未認証状態での全保護画面への直接アクセス", async ({ page }) => {
    for (const path of ["/projects", "/projects/1", "/projects/1/tasks", "/tasks/1"]) {
      await page.goto(path);
      await expect(page, `${path} から /login へリダイレクトされること`).toHaveURL("/login");
    }
  });
});
