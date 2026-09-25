import { bearer, expect, expectValidationError, NON_EXISTENT_ID, test, unique } from "../../support/fixtures";

const PROJECT_NOT_FOUND = { message: "指定されたプロジェクトが存在しません。" };

test.describe("5.3 プロジェクトAPI", () => {
  test("API-1001 プロジェクト一覧取得", async ({ api, data }) => {
    const user = await data.createUser();
    const project = await data.createProject(user);

    const response = await api.get("/api/projects", { headers: bearer(user) });

    expect(response.status()).toBe(200);
    expect(await response.json()).toEqual(expect.arrayContaining([project]));
  });

  test("API-1101 プロジェクト作成成功（全項目指定）", async ({ api, data }) => {
    const user = await data.createUser();
    const request = {
      name: unique("E2Eプロジェクト"),
      description: "全項目指定",
      status: "COMPLETED",
      startDate: "2026-10-01",
      endDate: "2026-12-31",
    };

    const response = await api.post("/api/projects", { headers: bearer(user), data: request });

    expect(response.status()).toBe(201);
    const body = await response.json();
    data.trackProject(body.id);
    expect(body).toEqual({ id: expect.any(Number), ...request });
  });

  test("API-1102 プロジェクト作成成功（日付未指定）", async ({ api, data }) => {
    const user = await data.createUser();

    const response = await api.post("/api/projects", {
      headers: bearer(user),
      data: { name: unique("E2Eプロジェクト") },
    });

    expect(response.status()).toBe(201);
    const body = await response.json();
    data.trackProject(body.id);
    expect(body.startDate).toBeNull();
    expect(body.endDate).toBeNull();
  });

  test("API-1103 作成者が自動的にOWNER登録される", async ({ api, data }) => {
    const user = await data.createUser();
    const project = await data.createProject(user);

    const response = await api.get(`/api/projects/${project.id}/members`, { headers: bearer(user) });

    expect(response.status()).toBe(200);
    expect(await response.json()).toEqual([
      { userId: user.id, name: user.name, email: user.email, role: "OWNER" },
    ]);
  });

  test("API-1104 name未指定", async ({ api, data }) => {
    const user = await data.createUser();

    const response = await api.post("/api/projects", { headers: bearer(user), data: { description: "名前なし" } });

    await expectValidationError(response, "name", "プロジェクト名は必須です。");
  });

  test("API-1105 name最大文字数超過", async ({ api, data }) => {
    const user = await data.createUser();

    const response = await api.post("/api/projects", { headers: bearer(user), data: { name: "あ".repeat(201) } });

    await expectValidationError(response, "name", "プロジェクト名は200文字以内で入力してください。");
  });

  test("API-1106 status不正値", async ({ api, data }) => {
    const user = await data.createUser();

    const response = await api.post("/api/projects", {
      headers: bearer(user),
      data: { name: unique("E2Eプロジェクト"), status: "INVALID" },
    });

    await expectValidationError(response, "status", "プロジェクト状態の値が不正です。");
  });

  test("API-1107 status省略時ACTIVEになる", async ({ api, data }) => {
    const user = await data.createUser();

    const response = await api.post("/api/projects", {
      headers: bearer(user),
      data: { name: unique("E2Eプロジェクト") },
    });

    expect(response.status()).toBe(201);
    const body = await response.json();
    data.trackProject(body.id);
    expect(body.status).toBe("ACTIVE");
  });

  test("API-1108 日付形式不正", async ({ api, data }) => {
    const user = await data.createUser();

    const response = await api.post("/api/projects", {
      headers: bearer(user),
      data: { name: unique("E2Eプロジェクト"), startDate: "invalid-date" },
    });

    await expectValidationError(response, "startDate", "入力値の形式が正しくありません。");
    // .NETの例外メッセージや型名などの内部情報を返さない
    expect(await response.text()).not.toMatch(/System\.|DateOnly|Exception|JSON value|Path:/);
  });

  test("API-1109 不正なJSON構文", async ({ api, data }) => {
    const user = await data.createUser();

    const response = await api.post("/api/projects", {
      headers: { ...bearer(user), "Content-Type": "application/json" },
      data: '{"name": "壊れたJSON",',
    });

    await expectValidationError(response, "body", "入力値の形式が正しくありません。");
    expect(await response.text()).not.toMatch(/System\.|Exception|JSON value|Path:|LineNumber/);
  });

  test("API-1201 プロジェクト詳細取得（存在する）", async ({ api, data }) => {
    const user = await data.createUser();
    const project = await data.createProject(user);

    const response = await api.get(`/api/projects/${project.id}`, { headers: bearer(user) });

    expect(response.status()).toBe(200);
    expect(await response.json()).toEqual(project);
  });

  test("API-1202 プロジェクト詳細取得（存在しない）", async ({ api, data }) => {
    const user = await data.createUser();

    const response = await api.get(`/api/projects/${NON_EXISTENT_ID}`, { headers: bearer(user) });

    expect(response.status()).toBe(404);
    expect(await response.json()).toEqual(PROJECT_NOT_FOUND);
  });

  test("API-1301 プロジェクト更新成功", async ({ api, data }) => {
    const user = await data.createUser();
    const project = await data.createProject(user);
    const request = {
      name: unique("E2E更新後"),
      description: "更新後の説明",
      status: "ARCHIVED",
      startDate: "2027-01-01",
      endDate: "2027-03-31",
    };

    const response = await api.put(`/api/projects/${project.id}`, { headers: bearer(user), data: request });

    expect(response.status()).toBe(200);
    expect(await response.json()).toEqual({ id: project.id, ...request });
  });

  test("API-1302 存在しないプロジェクトの更新", async ({ api, data }) => {
    const user = await data.createUser();

    const response = await api.put(`/api/projects/${NON_EXISTENT_ID}`, {
      headers: bearer(user),
      data: { name: unique("E2Eプロジェクト") },
    });

    expect(response.status()).toBe(404);
    expect(await response.json()).toEqual(PROJECT_NOT_FOUND);
  });

  test("API-1303 更新でstatus省略時に既存値維持", async ({ api, data }) => {
    const user = await data.createUser();
    const project = await data.createProject(user, { status: "COMPLETED" });

    const response = await api.put(`/api/projects/${project.id}`, {
      headers: bearer(user),
      data: { name: project.name, description: project.description },
    });

    expect(response.status()).toBe(200);
    expect((await response.json()).status).toBe("COMPLETED");
  });

  test("API-1401 プロジェクト削除成功", async ({ api, data }) => {
    const user = await data.createUser();
    const project = await data.createProject(user);

    const response = await api.delete(`/api/projects/${project.id}`, { headers: bearer(user) });

    expect(response.status()).toBe(204);
    expect(await response.body()).toHaveLength(0);
  });

  test("API-1402 存在しないプロジェクトの削除", async ({ api, data }) => {
    const user = await data.createUser();

    const response = await api.delete(`/api/projects/${NON_EXISTENT_ID}`, { headers: bearer(user) });

    expect(response.status()).toBe(404);
    expect(await response.json()).toEqual(PROJECT_NOT_FOUND);
  });

  test("API-1403 削除時のカスケード", async ({ api, data, db }) => {
    const owner = await data.createUser("オーナー");
    const member = await data.createUser("メンバー");
    const project = await data.createProject(owner);
    await data.addMember(project.id, owner, member);
    await data.createTask(project.id, owner, { assigneeId: member.id });

    const response = await api.delete(`/api/projects/${project.id}`, { headers: bearer(owner) });

    expect(response.status()).toBe(204);
    const { rows } = await db.query<{ members: number; tasks: number }>(
      `SELECT (SELECT count(*) FROM project_members WHERE project_id = $1)::int AS members,
              (SELECT count(*) FROM tasks WHERE project_id = $1)::int AS tasks`,
      [project.id],
    );
    expect(rows[0]).toEqual({ members: 0, tasks: 0 });
  });
});
