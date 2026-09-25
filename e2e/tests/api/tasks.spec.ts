import { bearer, expect, expectValidationError, NON_EXISTENT_ID, test, unique } from "../../support/fixtures";

const PROJECT_NOT_FOUND = { message: "指定されたプロジェクトが存在しません。" };
const TASK_NOT_FOUND = { message: "指定されたタスクが存在しません。" };

test.describe("5.5 タスクAPI", () => {
  test("API-1801 プロジェクト内タスク一覧取得", async ({ api, data }) => {
    const user = await data.createUser();
    const project = await data.createProject(user);
    const task1 = await data.createTask(project.id, user);
    const task2 = await data.createTask(project.id, user);

    const response = await api.get(`/api/projects/${project.id}/tasks`, { headers: bearer(user) });

    expect(response.status()).toBe(200);
    expect(await response.json()).toEqual([task1, task2]);
  });

  test("API-1802 存在しないプロジェクトのタスク一覧", async ({ api, data }) => {
    const user = await data.createUser();

    const response = await api.get(`/api/projects/${NON_EXISTENT_ID}/tasks`, { headers: bearer(user) });

    expect(response.status()).toBe(404);
    expect(await response.json()).toEqual(PROJECT_NOT_FOUND);
  });

  test("API-1901 タスク作成成功", async ({ api, data }) => {
    const user = await data.createUser();
    const project = await data.createProject(user);
    const request = {
      assigneeId: user.id,
      title: unique("E2Eタスク"),
      description: "タスクの説明",
      status: "IN_PROGRESS",
      priority: "HIGH",
      dueDate: "2026-11-15",
    };

    const response = await api.post(`/api/projects/${project.id}/tasks`, { headers: bearer(user), data: request });

    expect(response.status()).toBe(201);
    expect(await response.json()).toEqual({ id: expect.any(Number), projectId: project.id, ...request });
  });

  test("API-1902 title未指定", async ({ api, data }) => {
    const user = await data.createUser();
    const project = await data.createProject(user);

    const response = await api.post(`/api/projects/${project.id}/tasks`, {
      headers: bearer(user),
      data: { description: "タイトルなし" },
    });

    await expectValidationError(response, "title", "タスク名は必須です。");
  });

  test("API-1903 title最大文字数超過", async ({ api, data }) => {
    const user = await data.createUser();
    const project = await data.createProject(user);

    const response = await api.post(`/api/projects/${project.id}/tasks`, {
      headers: bearer(user),
      data: { title: "あ".repeat(201) },
    });

    await expectValidationError(response, "title", "タスク名は200文字以内で入力してください。");
  });

  test("API-1904 存在しない担当者指定", async ({ api, data }) => {
    const user = await data.createUser();
    const project = await data.createProject(user);

    const response = await api.post(`/api/projects/${project.id}/tasks`, {
      headers: bearer(user),
      data: { title: unique("E2Eタスク"), assigneeId: NON_EXISTENT_ID },
    });

    await expectValidationError(response, "assigneeId", "指定されたユーザーが存在しません。");
  });

  test("API-1905 status/priority不正値", async ({ api, data }) => {
    const user = await data.createUser();
    const project = await data.createProject(user);

    const response = await api.post(`/api/projects/${project.id}/tasks`, {
      headers: bearer(user),
      data: { title: unique("E2Eタスク"), status: "INVALID", priority: "INVALID" },
    });

    await expectValidationError(response, "status", "タスク状態の値が不正です。");
    await expectValidationError(response, "priority", "優先度の値が不正です。");
  });

  test("API-1906 存在しないプロジェクトへの作成", async ({ api, data }) => {
    const user = await data.createUser();

    const response = await api.post(`/api/projects/${NON_EXISTENT_ID}/tasks`, {
      headers: bearer(user),
      data: { title: unique("E2Eタスク") },
    });

    expect(response.status()).toBe(404);
    expect(await response.json()).toEqual(PROJECT_NOT_FOUND);
  });

  test("API-2001 タスク詳細取得（存在する）", async ({ api, data }) => {
    const user = await data.createUser();
    const project = await data.createProject(user);
    const task = await data.createTask(project.id, user);

    const response = await api.get(`/api/tasks/${task.id}`, { headers: bearer(user) });

    expect(response.status()).toBe(200);
    expect(await response.json()).toEqual(task);
  });

  test("API-2002 タスク詳細取得（存在しない）", async ({ api, data }) => {
    const user = await data.createUser();

    const response = await api.get(`/api/tasks/${NON_EXISTENT_ID}`, { headers: bearer(user) });

    expect(response.status()).toBe(404);
    expect(await response.json()).toEqual(TASK_NOT_FOUND);
  });

  test("API-2101 タスク更新成功", async ({ api, data }) => {
    const user = await data.createUser();
    const project = await data.createProject(user);
    const task = await data.createTask(project.id, user);
    const request = {
      assigneeId: user.id,
      title: unique("E2E更新後"),
      description: "更新後の説明",
      status: "DONE",
      priority: "LOW",
      dueDate: "2026-12-01",
    };

    const response = await api.put(`/api/tasks/${task.id}`, { headers: bearer(user), data: request });

    expect(response.status()).toBe(200);
    expect(await response.json()).toEqual({ id: task.id, projectId: project.id, ...request });
  });

  test("API-2102 存在しないタスクの更新", async ({ api, data }) => {
    const user = await data.createUser();

    const response = await api.put(`/api/tasks/${NON_EXISTENT_ID}`, {
      headers: bearer(user),
      data: { title: unique("E2Eタスク") },
    });

    expect(response.status()).toBe(404);
    expect(await response.json()).toEqual(TASK_NOT_FOUND);
  });

  test("API-2201 タスク削除成功", async ({ api, data }) => {
    const user = await data.createUser();
    const project = await data.createProject(user);
    const task = await data.createTask(project.id, user);

    const response = await api.delete(`/api/tasks/${task.id}`, { headers: bearer(user) });

    expect(response.status()).toBe(204);
    expect((await api.get(`/api/tasks/${task.id}`, { headers: bearer(user) })).status()).toBe(404);
  });

  test("API-2202 存在しないタスクの削除", async ({ api, data }) => {
    const user = await data.createUser();

    const response = await api.delete(`/api/tasks/${NON_EXISTENT_ID}`, { headers: bearer(user) });

    expect(response.status()).toBe(404);
    expect(await response.json()).toEqual(TASK_NOT_FOUND);
  });

  test("API-2203 削除時のカスケード", async ({ api, data, db }) => {
    const user = await data.createUser();
    const project = await data.createProject(user);
    const task = await data.createTask(project.id, user);
    await data.createComment(task.id, user);
    await data.createComment(task.id, user);

    const response = await api.delete(`/api/tasks/${task.id}`, { headers: bearer(user) });

    expect(response.status()).toBe(204);
    const { rows } = await db.query<{ count: number }>(
      "SELECT count(*)::int AS count FROM task_comments WHERE task_id = $1",
      [task.id],
    );
    expect(rows[0].count).toBe(0);
  });
});
