import { DatabaseError } from "pg";
import { API_URL, WEB_URL } from "../../support/env";
import { bearer, expect, PASSWORD, test, unique } from "../../support/fixtures";
import { loginViaUi } from "../../support/ui";

/** 一意制約違反(PostgreSQLのエラーコード 23505)で失敗することを検証する */
async function expectUniqueViolation(query: Promise<unknown>, constraint: string): Promise<void> {
  const error = await query.then(
    () => null,
    (e: unknown) => e,
  );
  expect(error, "一意制約違反でエラーになること").toBeInstanceOf(DatabaseError);
  expect((error as DatabaseError).code).toBe("23505");
  expect((error as DatabaseError).constraint).toBe(constraint);
}

test.describe("6. 結合テスト", () => {
  test("IT-001 プロジェクト削除のカスケード", async ({ api, data, db }) => {
    const owner = await data.createUser("オーナー");
    const member = await data.createUser("メンバー");
    const project = await data.createProject(owner);
    await data.addMember(project.id, owner, member);
    const task = await data.createTask(project.id, owner, { assigneeId: member.id });
    await data.createComment(task.id, member);

    const response = await api.delete(`/api/projects/${project.id}`, { headers: bearer(owner) });

    expect(response.status()).toBe(204);
    const { rows } = await db.query(
      `SELECT (SELECT count(*) FROM projects WHERE id = $1)::int AS projects,
              (SELECT count(*) FROM project_members WHERE project_id = $1)::int AS members,
              (SELECT count(*) FROM tasks WHERE project_id = $1)::int AS tasks,
              (SELECT count(*) FROM task_comments WHERE task_id = $2)::int AS comments`,
      [project.id, task.id],
    );
    expect(rows[0]).toEqual({ projects: 0, members: 0, tasks: 0, comments: 0 });
  });

  test("IT-002 タスク削除のカスケード", async ({ api, data, db }) => {
    const user = await data.createUser();
    const project = await data.createProject(user);
    const task = await data.createTask(project.id, user);
    await data.createComment(task.id, user);

    const response = await api.delete(`/api/tasks/${task.id}`, { headers: bearer(user) });

    expect(response.status()).toBe(204);
    const { rows } = await db.query("SELECT count(*)::int AS count FROM task_comments WHERE task_id = $1", [task.id]);
    expect(rows[0].count).toBe(0);
  });

  test("IT-003 ユーザー削除時の担当タスクの扱い", async ({ data, db }) => {
    const owner = await data.createUser("オーナー");
    const assignee = await data.createUser("担当者");
    const project = await data.createProject(owner);
    const task = await data.createTask(project.id, owner, { assigneeId: assignee.id });

    await db.query("DELETE FROM users WHERE id = $1", [assignee.id]);

    const { rows } = await db.query<{ assignee_id: string | null }>(
      "SELECT assignee_id FROM tasks WHERE id = $1",
      [task.id],
    );
    expect(rows).toHaveLength(1);
    expect(rows[0].assignee_id).toBeNull();
  });

  test("IT-004 プロジェクト作成のトランザクション性", async ({ api, data, db }) => {
    // JWTを発行した後にユーザーをDBから削除すると、トークンは有効なまま作成者IDだけが存在しない状態になる。
    // この状態でプロジェクトを作成すると、projectsへの登録は成功し、
    // 続くproject_membersへの登録だけが外部キー違反で失敗する。
    const user = await data.createUser();
    await db.query("DELETE FROM users WHERE id = $1", [user.id]);
    const name = unique("E2Eロールバック確認");

    const response = await api.post("/api/projects", { headers: bearer(user), data: { name } });

    expect(response.status()).toBe(500);
    const { rows } = await db.query(
      `SELECT (SELECT count(*) FROM projects WHERE name = $1)::int AS projects,
              (SELECT count(*) FROM project_members WHERE user_id = $2)::int AS members`,
      [name, user.id],
    );
    expect(rows[0]).toEqual({ projects: 0, members: 0 });
  });

  test("IT-005 メールアドレスの一意制約", async ({ data, db }) => {
    const user = await data.createUser();

    await expectUniqueViolation(
      db.query("INSERT INTO users (name, email, password_hash) VALUES ($1, $2, $3)", [
        unique("E2E重複"),
        user.email,
        "dummy-hash",
      ]),
      "users_email_key",
    );
  });

  test("IT-006 プロジェクトメンバーの重複防止制約", async ({ data, db }) => {
    const owner = await data.createUser();
    const project = await data.createProject(owner);

    await expectUniqueViolation(
      db.query("INSERT INTO project_members (project_id, user_id, role) VALUES ($1, $2, 'MEMBER')", [
        project.id,
        owner.id,
      ]),
      "uq_project_members_project_user",
    );
  });

  test("IT-007 updated_atの自動更新がDBに反映される", async ({ api, data, db }) => {
    const user = await data.createUser();
    const project = await data.createProject(user);
    const task = await data.createTask(project.id, user);

    const projectResponse = await api.put(`/api/projects/${project.id}`, {
      headers: bearer(user),
      data: { ...project, name: unique("E2E更新後") },
    });
    const taskResponse = await api.put(`/api/tasks/${task.id}`, {
      headers: bearer(user),
      data: { ...task, title: unique("E2E更新後") },
    });
    expect(projectResponse.status()).toBe(200);
    expect(taskResponse.status()).toBe(200);

    // タイムゾーンの解釈差をなくすため、比較はDB上で行う(created_at/updated_atはUTCで保存される)
    for (const table of ["projects", "tasks"] as const) {
      const id = table === "projects" ? project.id : task.id;
      const { rows } = await db.query<{ changed: boolean; seconds_from_now: number }>(
        `SELECT updated_at <> created_at AS changed,
                abs(extract(epoch FROM (now() AT TIME ZONE 'UTC') - updated_at))::float AS seconds_from_now
           FROM ${table} WHERE id = $1`,
        [id],
      );
      expect(rows[0].changed, `${table}.updated_at が created_at と異なること`).toBe(true);
      expect(rows[0].seconds_from_now, `${table}.updated_at が更新実行時刻に近いこと`).toBeLessThan(60);
    }
  });

  test("IT-008 CORS設定", async ({ api, data, page }) => {
    const user = await data.createUser();

    // プリフライト: フロントエンドのオリジンからのPOST(JSON・Authorizationヘッダー付き)が許可される
    const preflight = await api.fetch("/api/projects", {
      method: "OPTIONS",
      headers: {
        Origin: WEB_URL,
        "Access-Control-Request-Method": "POST",
        "Access-Control-Request-Headers": "authorization,content-type",
      },
    });
    expect(preflight.status()).toBe(204);
    expect(preflight.headers()["access-control-allow-origin"]).toBe(WEB_URL);

    // 本リクエスト: ブラウザ上のフロントエンドから実際にAPIを呼び出して成功する
    const loginResponse = page.waitForResponse(
      (response) => response.url() === `${API_URL}/api/auth/login` && response.request().method() === "POST",
    );
    await loginViaUi(page, { ...user, password: PASSWORD });
    expect((await loginResponse).status()).toBe(200);
    await expect(page.getByRole("heading", { name: "プロジェクト一覧" })).toBeVisible();
  });
});
