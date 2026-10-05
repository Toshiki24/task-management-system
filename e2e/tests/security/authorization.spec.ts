import { bearer, expect, expectValidationError, test, unique, type TestDataFactory } from "../../support/fixtures";
import { alertMessage, modal, signIn, tableRow } from "../../support/ui";

const PROJECT_NOT_FOUND = { message: "指定されたプロジェクトが存在しません。" };
const TASK_NOT_FOUND = { message: "指定されたタスクが存在しません。" };
const FORBIDDEN = { message: "この操作を行う権限がありません。" };

const PROJECT_BODY = {
  name: "書き換え後のプロジェクト名",
  description: "書き換え後の説明",
  status: "ARCHIVED",
  startDate: "2026-10-01",
  endDate: "2026-12-31",
};

const TASK_BODY = {
  title: "書き換え後のタスク名",
  description: "書き換え後の説明",
  status: "DONE",
  priority: "LOW",
  dueDate: "2026-10-31",
};

test.describe("9.1 SEC-01 認可チェック（プロジェクトに所属していないユーザー）", () => {
  test("SEC-01-01 所属していないワークスペースのプロジェクトは一覧・取得できない", async ({ api, data }) => {
    const owner = await data.createUser("オーナー");
    const outsider = await data.createUser("非メンバー");
    const othersProject = await data.createProject(owner);
    const ownProject = await data.createProject(outsider);

    // 自分の所属ワークスペースの一覧には自分のプロジェクトだけが含まれる
    const ownList = await api.get(`/api/workspaces/${ownProject.workspaceId}/projects`, { headers: bearer(outsider) });
    expect(ownList.status()).toBe(200);
    const ids = (await ownList.json()).map((p: { id: number }) => p.id);
    expect(ids).toContain(ownProject.id);
    expect(ids).not.toContain(othersProject.id);

    // 所属していないワークスペースの一覧は404(存在を開示しない)
    const othersList = await api.get(`/api/workspaces/${othersProject.workspaceId}/projects`, { headers: bearer(outsider) });
    expect(othersList.status()).toBe(404);
  });

  test("SEC-01-02 所属していないプロジェクトの詳細取得", async ({ api, data }) => {
    const owner = await data.createUser("オーナー");
    const outsider = await data.createUser("非メンバー");
    const project = await data.createProject(owner);

    const response = await api.get(`/api/projects/${project.id}`, { headers: bearer(outsider) });

    expect(response.status()).toBe(404);
    expect(await response.json()).toEqual(PROJECT_NOT_FOUND);
  });

  test("SEC-01-03 所属していないプロジェクトの更新", async ({ api, data, db }) => {
    const owner = await data.createUser("オーナー");
    const outsider = await data.createUser("非メンバー");
    const project = await data.createProject(owner);

    const response = await api.put(`/api/projects/${project.id}`, { headers: bearer(outsider), data: PROJECT_BODY });

    expect(response.status()).toBe(404);
    expect(await response.json()).toEqual(PROJECT_NOT_FOUND);
    const { rows } = await db.query<{ name: string }>("SELECT name FROM projects WHERE id = $1", [project.id]);
    expect(rows[0].name).toBe(project.name);
  });

  test("SEC-01-04 所属していないプロジェクトの削除", async ({ api, data, db }) => {
    const owner = await data.createUser("オーナー");
    const outsider = await data.createUser("非メンバー");
    const project = await data.createProject(owner);

    const response = await api.delete(`/api/projects/${project.id}`, { headers: bearer(outsider) });

    expect(response.status()).toBe(404);
    expect(await response.json()).toEqual(PROJECT_NOT_FOUND);
    const { rowCount } = await db.query("SELECT 1 FROM projects WHERE id = $1", [project.id]);
    expect(rowCount).toBe(1);
  });

  test("SEC-01-05 所属していないプロジェクトのメンバー一覧取得", async ({ api, data }) => {
    const owner = await data.createUser("オーナー");
    const outsider = await data.createUser("非メンバー");
    const project = await data.createProject(owner);

    const response = await api.get(`/api/projects/${project.id}/members`, { headers: bearer(outsider) });

    expect(response.status()).toBe(404);
    expect(await response.json()).toEqual(PROJECT_NOT_FOUND);
  });

  test("SEC-01-06 所属していないプロジェクトに自分自身をメンバー追加", async ({ api, data, db }) => {
    const owner = await data.createUser("オーナー");
    const outsider = await data.createUser("非メンバー");
    const project = await data.createProject(owner);

    const response = await api.post(`/api/projects/${project.id}/members`, {
      headers: bearer(outsider),
      data: { userId: outsider.id, role: "OWNER" },
    });

    expect(response.status()).toBe(404);
    expect(await response.json()).toEqual(PROJECT_NOT_FOUND);
    const { rowCount } = await db.query(
      "SELECT 1 FROM project_members WHERE project_id = $1 AND user_id = $2",
      [project.id, outsider.id],
    );
    expect(rowCount).toBe(0);
  });

  test("SEC-01-07 所属していないプロジェクトのメンバー削除", async ({ api, data, db }) => {
    const owner = await data.createUser("オーナー");
    const outsider = await data.createUser("非メンバー");
    const project = await data.createProject(owner);

    const response = await api.delete(`/api/projects/${project.id}/members/${owner.id}`, {
      headers: bearer(outsider),
    });

    expect(response.status()).toBe(404);
    expect(await response.json()).toEqual(PROJECT_NOT_FOUND);
    const { rowCount } = await db.query(
      "SELECT 1 FROM project_members WHERE project_id = $1 AND user_id = $2",
      [project.id, owner.id],
    );
    expect(rowCount).toBe(1);
  });

  test("SEC-01-08 所属していないプロジェクトのタスク一覧取得・タスク登録", async ({ api, data, db }) => {
    const owner = await data.createUser("オーナー");
    const outsider = await data.createUser("非メンバー");
    const project = await data.createProject(owner);

    const listResponse = await api.get(`/api/projects/${project.id}/tasks`, { headers: bearer(outsider) });
    const createResponse = await api.post(`/api/projects/${project.id}/tasks`, {
      headers: bearer(outsider),
      data: { ...TASK_BODY, title: unique("E2Eタスク") },
    });

    expect(listResponse.status()).toBe(404);
    expect(await listResponse.json()).toEqual(PROJECT_NOT_FOUND);
    expect(createResponse.status()).toBe(404);
    expect(await createResponse.json()).toEqual(PROJECT_NOT_FOUND);
    const { rowCount } = await db.query("SELECT 1 FROM tasks WHERE project_id = $1", [project.id]);
    expect(rowCount).toBe(0);
  });

  test("SEC-01-09 所属していないプロジェクトのタスク詳細取得", async ({ api, data }) => {
    const owner = await data.createUser("オーナー");
    const outsider = await data.createUser("非メンバー");
    const project = await data.createProject(owner);
    const task = await data.createTask(project.id, owner);

    const response = await api.get(`/api/tasks/${task.id}`, { headers: bearer(outsider) });

    expect(response.status()).toBe(404);
    expect(await response.json()).toEqual(TASK_NOT_FOUND);
  });

  test("SEC-01-10 所属していないプロジェクトのタスク更新", async ({ api, data, db }) => {
    const owner = await data.createUser("オーナー");
    const outsider = await data.createUser("非メンバー");
    const project = await data.createProject(owner);
    const task = await data.createTask(project.id, owner);

    const response = await api.put(`/api/tasks/${task.id}`, { headers: bearer(outsider), data: TASK_BODY });

    expect(response.status()).toBe(404);
    expect(await response.json()).toEqual(TASK_NOT_FOUND);
    const { rows } = await db.query<{ title: string }>("SELECT title FROM tasks WHERE id = $1", [task.id]);
    expect(rows[0].title).toBe(task.title);
  });

  test("SEC-01-11 所属していないプロジェクトのタスク削除", async ({ api, data, db }) => {
    const owner = await data.createUser("オーナー");
    const outsider = await data.createUser("非メンバー");
    const project = await data.createProject(owner);
    const task = await data.createTask(project.id, owner);

    const response = await api.delete(`/api/tasks/${task.id}`, { headers: bearer(outsider) });

    expect(response.status()).toBe(404);
    expect(await response.json()).toEqual(TASK_NOT_FOUND);
    const { rowCount } = await db.query("SELECT 1 FROM tasks WHERE id = $1", [task.id]);
    expect(rowCount).toBe(1);
  });

  test("SEC-01-12 所属していないプロジェクトのコメント一覧取得・コメント登録", async ({ api, data, db }) => {
    const owner = await data.createUser("オーナー");
    const outsider = await data.createUser("非メンバー");
    const project = await data.createProject(owner);
    const task = await data.createTask(project.id, owner);

    const listResponse = await api.get(`/api/tasks/${task.id}/comments`, { headers: bearer(outsider) });
    const createResponse = await api.post(`/api/tasks/${task.id}/comments`, {
      headers: bearer(outsider),
      data: { comment: unique("E2Eコメント") },
    });

    expect(listResponse.status()).toBe(404);
    expect(await listResponse.json()).toEqual(TASK_NOT_FOUND);
    expect(createResponse.status()).toBe(404);
    expect(await createResponse.json()).toEqual(TASK_NOT_FOUND);
    const { rowCount } = await db.query("SELECT 1 FROM task_comments WHERE task_id = $1", [task.id]);
    expect(rowCount).toBe(0);
  });
});

test.describe("9.2 SEC-01 認可チェック（プロジェクト内権限）", () => {
  /** オーナーが作成したプロジェクトに、MEMBERとして参加しているユーザーを用意する */
  async function setUpMember(data: TestDataFactory) {
    const owner = await data.createUser("オーナー");
    const member = await data.createUser("メンバー");
    const project = await data.createProject(owner);
    await data.addMember(project.id, owner, member, "MEMBER");
    return { owner, member, project };
  }

  test("SEC-01-13 MEMBERによるプロジェクト更新", async ({ api, data, db }) => {
    const { member, project } = await setUpMember(data);

    const response = await api.put(`/api/projects/${project.id}`, { headers: bearer(member), data: PROJECT_BODY });

    expect(response.status()).toBe(403);
    expect(await response.json()).toEqual(FORBIDDEN);
    const { rows } = await db.query<{ name: string }>("SELECT name FROM projects WHERE id = $1", [project.id]);
    expect(rows[0].name).toBe(project.name);
  });

  test("SEC-01-14 MEMBERによるプロジェクト削除", async ({ api, data, db }) => {
    const { member, project } = await setUpMember(data);

    const response = await api.delete(`/api/projects/${project.id}`, { headers: bearer(member) });

    expect(response.status()).toBe(403);
    expect(await response.json()).toEqual(FORBIDDEN);
    const { rowCount } = await db.query("SELECT 1 FROM projects WHERE id = $1", [project.id]);
    expect(rowCount).toBe(1);
  });

  test("SEC-01-15 MEMBERによるメンバー追加", async ({ api, data, db }) => {
    const { member, project } = await setUpMember(data);
    const user = await data.createUser("追加対象");

    const response = await api.post(`/api/projects/${project.id}/members`, {
      headers: bearer(member),
      data: { userId: user.id, role: "MEMBER" },
    });

    expect(response.status()).toBe(403);
    expect(await response.json()).toEqual(FORBIDDEN);
    const { rowCount } = await db.query(
      "SELECT 1 FROM project_members WHERE project_id = $1 AND user_id = $2",
      [project.id, user.id],
    );
    expect(rowCount).toBe(0);
  });

  test("SEC-01-16 MEMBERによるメンバー削除", async ({ api, data, db }) => {
    const { owner, member, project } = await setUpMember(data);

    const response = await api.delete(`/api/projects/${project.id}/members/${owner.id}`, {
      headers: bearer(member),
    });

    expect(response.status()).toBe(403);
    expect(await response.json()).toEqual(FORBIDDEN);
    const { rowCount } = await db.query(
      "SELECT 1 FROM project_members WHERE project_id = $1 AND user_id = $2",
      [project.id, owner.id],
    );
    expect(rowCount).toBe(1);
  });

  test("SEC-01-17 VIEWERによるタスク削除は拒否される", async ({ api, data, db }) => {
    const owner = await data.createUser("オーナー");
    const viewer = await data.createUser("閲覧者");
    const project = await data.createProject(owner);
    // Viewer はワークスペースの閲覧専用ロール(プロジェクトは見えるが書き込み不可)
    await data.addWorkspaceMember(project.workspaceId, viewer, "VIEWER");
    const task = await data.createTask(project.id, owner);

    const response = await api.delete(`/api/tasks/${task.id}`, { headers: bearer(viewer) });

    expect(response.status()).toBe(403);
    expect(await response.json()).toEqual(FORBIDDEN);
    const { rowCount } = await db.query("SELECT 1 FROM tasks WHERE id = $1", [task.id]);
    expect(rowCount).toBe(1);
  });

  test("SEC-01-18 MEMBERが許可された操作を行える", async ({ api, data }) => {
    const { owner, member, project } = await setUpMember(data);
    const task = await data.createTask(project.id, owner);
    const headers = bearer(member);

    // 参照系
    expect((await api.get(`/api/workspaces/${project.workspaceId}/projects`, { headers })).status()).toBe(200);
    expect((await api.get(`/api/projects/${project.id}`, { headers })).status()).toBe(200);
    expect((await api.get(`/api/projects/${project.id}/members`, { headers })).status()).toBe(200);
    expect((await api.get(`/api/projects/${project.id}/tasks`, { headers })).status()).toBe(200);
    expect((await api.get(`/api/tasks/${task.id}`, { headers })).status()).toBe(200);
    expect((await api.get(`/api/tasks/${task.id}/comments`, { headers })).status()).toBe(200);

    // 更新系(タスク登録・更新・削除、コメント登録)。WS Member は書き込み可(設計 §3.3)
    const created = await api.post(`/api/projects/${project.id}/tasks`, {
      headers,
      data: { ...TASK_BODY, title: unique("E2Eタスク"), assigneeId: member.id },
    });
    expect(created.status()).toBe(201);
    const updated = await api.put(`/api/tasks/${task.id}`, { headers, data: { ...TASK_BODY, assigneeId: member.id } });
    expect(updated.status()).toBe(200);
    const commented = await api.post(`/api/tasks/${task.id}/comments`, {
      headers,
      data: { comment: unique("E2Eコメント") },
    });
    expect(commented.status()).toBe(201);
    // Member はタスクを削除できる(作成した別タスクで確認)
    const deletable = await data.createTask(project.id, owner);
    const deleted = await api.delete(`/api/tasks/${deletable.id}`, { headers });
    expect(deleted.status()).toBe(204);
  });

  test("SEC-01-19 作成者以外のOWNERがOWNER権限の操作を行える", async ({ api, data }) => {
    const creator = await data.createUser("作成者");
    const secondOwner = await data.createUser("追加OWNER");
    const member = await data.createUser("メンバー");
    const project = await data.createProject(creator);
    await data.addMember(project.id, creator, secondOwner, "OWNER");
    await data.addMember(project.id, creator, member, "MEMBER");
    const task = await data.createTask(project.id, creator);
    const headers = bearer(secondOwner);

    const updated = await api.put(`/api/projects/${project.id}`, { headers, data: PROJECT_BODY });
    expect(updated.status()).toBe(200);
    const removed = await api.delete(`/api/projects/${project.id}/members/${member.id}`, { headers });
    expect(removed.status()).toBe(204);
    const taskDeleted = await api.delete(`/api/tasks/${task.id}`, { headers });
    expect(taskDeleted.status()).toBe(204);
    const projectDeleted = await api.delete(`/api/projects/${project.id}`, { headers });
    expect(projectDeleted.status()).toBe(204);
  });
});

test.describe("9.3 SEC-10 タスク担当者の所属チェック", () => {
  const NOT_MEMBER_MESSAGE = "指定されたユーザーはプロジェクトのメンバーではありません。";

  async function setUp(data: TestDataFactory) {
    const owner = await data.createUser("オーナー");
    const outsider = await data.createUser("非メンバー");
    const project = await data.createProject(owner);
    return { owner, outsider, project };
  }

  test("SEC-10-01 所属していないユーザーを担当者にしてタスク登録", async ({ api, data, db }) => {
    const { owner, outsider, project } = await setUp(data);

    const response = await api.post(`/api/projects/${project.id}/tasks`, {
      headers: bearer(owner),
      data: { ...TASK_BODY, title: unique("E2Eタスク"), assigneeId: outsider.id },
    });

    await expectValidationError(response, "assigneeId", NOT_MEMBER_MESSAGE);
    const { rowCount } = await db.query("SELECT 1 FROM tasks WHERE project_id = $1", [project.id]);
    expect(rowCount).toBe(0);
  });

  test("SEC-10-02 所属していないユーザーを担当者にしてタスク更新", async ({ api, data, db }) => {
    const { owner, outsider, project } = await setUp(data);
    const task = await data.createTask(project.id, owner);

    const response = await api.put(`/api/tasks/${task.id}`, {
      headers: bearer(owner),
      data: { ...TASK_BODY, assigneeId: outsider.id },
    });

    await expectValidationError(response, "assigneeId", NOT_MEMBER_MESSAGE);
    const { rows } = await db.query<{ assignee_id: string | null }>(
      "SELECT assignee_id FROM tasks WHERE id = $1",
      [task.id],
    );
    expect(rows[0].assignee_id).toBeNull();
  });
});

test.describe("9.4 SEC-01 認可チェック（画面）", () => {
  test("SEC-01-20 プロジェクト一覧画面に所属していないプロジェクトが表示されない", async ({ page, data }) => {
    const owner = await data.createUser("オーナー");
    const outsider = await data.createUser("非メンバー");
    const othersProject = await data.createProject(owner);
    const ownProject = await data.createProject(outsider);
    await signIn(page, outsider);

    await page.goto("/projects");

    await expect(tableRow(page, ownProject.name)).toBeVisible();
    await expect(tableRow(page, othersProject.name)).toHaveCount(0);
  });

  test("SEC-01-21 所属していないプロジェクトの詳細画面にURLで直接アクセス", async ({ page, data }) => {
    const owner = await data.createUser("オーナー");
    const outsider = await data.createUser("非メンバー");
    const project = await data.createProject(owner);
    await signIn(page, outsider);

    await page.goto(`/projects/${project.id}`);

    await expect(alertMessage(page)).toHaveText("プロジェクト情報の取得に失敗しました。");
    await expect(page.getByText(project.name)).toHaveCount(0);
  });

  test("SEC-01-22 MEMBERがプロジェクト削除を実行すると権限エラーが表示される", async ({ page, data, db }) => {
    const owner = await data.createUser("オーナー");
    const member = await data.createUser("メンバー");
    const project = await data.createProject(owner);
    await data.addMember(project.id, owner, member, "MEMBER");
    await signIn(page, member);
    await page.goto(`/projects/${project.id}`);

    await page.getByRole("button", { name: "削除", exact: true }).first().click();
    await modal(page, "プロジェクト削除").getByRole("button", { name: "削除" }).click();

    await expect(alertMessage(page)).toHaveText(FORBIDDEN.message);
    const { rowCount } = await db.query("SELECT 1 FROM projects WHERE id = $1", [project.id]);
    expect(rowCount).toBe(1);
  });
});
