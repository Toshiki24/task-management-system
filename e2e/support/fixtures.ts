import { createHash, randomUUID } from "node:crypto";
import { test as base, expect, type APIRequestContext, type APIResponse } from "@playwright/test";
import bcrypt from "bcryptjs";
import pg from "pg";
import { API_URL, DB_CONFIG, ORIGIN_VERIFY_SECRET } from "./env";

export const PASSWORD = "Password123!";

/** テスト用DBでは採番されない大きさのID(存在しないリソースの指定に使う) */
export const NON_EXISTENT_ID = 999_999_999;

export interface TestUser {
  id: number;
  name: string;
  email: string;
  password: string;
  token: string;
}

export interface Project {
  id: number;
  workspaceId: number;
  name: string;
  description: string | null;
  status: string;
  startDate: string | null;
  endDate: string | null;
}

export interface Task {
  id: number;
  projectId: number;
  assigneeId: number | null;
  title: string;
  description: string | null;
  status: string;
  priority: string;
  dueDate: string | null;
  boardPosition: number;
  estimatePoints: number | null;
  labels: { id: number; workspaceId: number; name: string; color: string | null }[];
}

export interface CreatedComment {
  id: number;
  taskId: number;
  userId: number;
  comment: string;
  createdAt: string;
}

export function unique(prefix: string): string {
  return `${prefix}-${randomUUID().slice(0, 12)}`;
}

export function bearer(user: TestUser): Record<string, string> {
  return { Authorization: `Bearer ${user.token}` };
}

/** API仕様書の共通バリデーションエラー形式で、指定フィールドのエラーが返ることを検証する */
export async function expectValidationError(
  response: APIResponse,
  field: string,
  message?: string,
): Promise<void> {
  expect(response.status()).toBe(400);
  const body = await response.json();
  expect(body.message).toBe("入力内容に誤りがあります。");
  expect(body.errors).toEqual(
    expect.arrayContaining([message ? { field, message } : expect.objectContaining({ field })]),
  );
}

/**
 * テストデータの作成と後片付けを行う。
 *
 * - ユーザーはテストごとに一意なメールアドレスでDBへ直接登録する(ユーザー登録APIがないため)
 * - プロジェクト・タスク・コメントは通常のAPI経由で作成する
 * - テスト終了時、そのテストで作成したユーザーと、そのユーザーが所属するプロジェクトを削除する
 *   (タスク・コメント・メンバーはON DELETE CASCADEで連動して削除される)
 *
 * 他のテストが作成したデータには触れないため、並列実行しても結果が変わらない。
 */
export class TestDataFactory {
  private readonly userIds: number[] = [];
  private readonly projectIds: number[] = [];
  private readonly workspaceIds: number[] = [];

  constructor(
    private readonly api: APIRequestContext,
    private readonly db: pg.Pool,
  ) {}

  /**
   * ワークスペースを DB に直接作成し、指定ユーザーをメンバーとして登録する。
   * (ワークスペース作成 API は System Admin 専用のため、テストでは DB 直挿しで用意する)
   */
  async createWorkspace(owner: TestUser, role = "ADMIN"): Promise<number> {
    const { rows } = await this.db.query<{ id: string }>(
      "INSERT INTO workspaces (name) VALUES ($1) RETURNING id",
      [unique("E2Eワークスペース")],
    );
    const id = Number(rows[0].id);
    this.workspaceIds.push(id);
    await this.seedDefaultWorkflow(id);
    await this.addWorkspaceMember(id, owner, role);
    return id;
  }

  /**
   * 既定ワークフロー(TODO/IN_PROGRESS/DONE)を DB 直挿しで用意する。
   * (本番・アプリ経由の作成では自動で用意されるが、テストでは WS を DB 直挿しするため合わせる。M2 §3)
   */
  async seedDefaultWorkflow(workspaceId: number): Promise<void> {
    await this.db.query(
      `INSERT INTO workflow_states (workspace_id, key, name, category, position, is_default)
       VALUES ($1, 'TODO', '未着手', 'TODO', 0, true),
              ($1, 'IN_PROGRESS', '対応中', 'IN_PROGRESS', 1, false),
              ($1, 'DONE', '完了', 'DONE', 2, false)
       ON CONFLICT (workspace_id, key) DO NOTHING`,
      [workspaceId],
    );
  }

  /** ユーザーを System Admin にする(DB 直挿し)。 */
  async makeSystemAdmin(user: TestUser): Promise<void> {
    await this.db.query("UPDATE users SET is_system_admin = true WHERE id = $1", [user.id]);
  }

  /**
   * ワークスペースへの招待を DB に直接作成し、受諾用の平文トークンを返す。
   * (トークンはメールでのみ配布され API レスポンスには含まれないため、画面テストでは DB 直挿しで用意する)
   */
  async createInvitation(
    workspaceId: number,
    email: string,
    invitedBy: TestUser,
    role = "MEMBER",
  ): Promise<string> {
    const token = randomUUID();
    const tokenHash = createHash("sha256").update(token).digest("hex");
    await this.db.query(
      "INSERT INTO invitations (workspace_id, email, role, token_hash, expires_at, invited_by) "
        + "VALUES ($1, $2, $3, $4, now() + interval '7 days', $5)",
      [workspaceId, email, role, tokenHash, invitedBy.id],
    );
    return token;
  }

  /** 既存ユーザーをワークスペースのメンバーにする(DB 直挿し。重複は無視) */
  async addWorkspaceMember(workspaceId: number, member: TestUser, role = "MEMBER"): Promise<void> {
    await this.db.query(
      `INSERT INTO workspace_members (workspace_id, user_id, role) VALUES ($1, $2, $3)
       ON CONFLICT (workspace_id, user_id) DO NOTHING`,
      [workspaceId, member.id, role],
    );
  }

  async createUser(label = "ユーザー"): Promise<TestUser> {
    const suffix = randomUUID().slice(0, 12);
    const name = `E2E${label}-${suffix}`;
    const email = `e2e-${suffix}@example.test`;
    // テスト実行時間短縮のためワークファクタを下げる(照合処理はアプリ側の実装そのまま)
    const passwordHash = await bcrypt.hash(PASSWORD, 4);

    const { rows } = await this.db.query<{ id: string }>(
      "INSERT INTO users (name, email, password_hash) VALUES ($1, $2, $3) RETURNING id",
      [name, email, passwordHash],
    );
    const id = Number(rows[0].id);
    this.userIds.push(id);

    return { id, name, email, password: PASSWORD, token: await this.login(email, PASSWORD) };
  }

  async login(email: string, password: string): Promise<string> {
    const response = await this.api.post("/api/auth/login", { data: { email, password } });
    expect(response.status(), "テストユーザーでログインできること").toBe(200);
    return (await response.json()).accessToken;
  }

  async createProject(
    owner: TestUser,
    body: Partial<Omit<Project, "id" | "workspaceId">> & { workspaceId?: number } = {},
  ): Promise<Project> {
    // プロジェクトは必ずワークスペースに属する。未指定なら owner を Admin とする WS を用意する
    const { workspaceId: givenWorkspaceId, ...projectBody } = body;
    const workspaceId = givenWorkspaceId ?? (await this.createWorkspace(owner, "ADMIN"));

    const response = await this.api.post(`/api/workspaces/${workspaceId}/projects`, {
      headers: bearer(owner),
      data: {
        name: unique("E2Eプロジェクト"),
        description: "E2Eテスト用プロジェクト",
        status: "ACTIVE",
        startDate: "2026-10-01",
        endDate: "2026-12-31",
        ...projectBody,
      },
    });
    expect(response.status(), "テスト用プロジェクトを作成できること").toBe(201);
    const project: Project = await response.json();
    this.trackProject(project.id);
    return project;
  }

  /** 画面操作で作成したプロジェクトなど、後片付け対象に加える */
  trackProject(id: number): void {
    this.projectIds.push(id);
  }

  async addMember(projectId: number, actor: TestUser, member: TestUser, role = "MEMBER"): Promise<void> {
    // 可視性はワークスペース所属で決まるため、プロジェクトの所属 WS のメンバーにもしておく
    const { rows } = await this.db.query<{ workspace_id: string }>(
      "SELECT workspace_id FROM projects WHERE id = $1",
      [projectId],
    );
    if (rows.length > 0) {
      const workspaceRole = role === "OWNER" ? "ADMIN" : "MEMBER";
      await this.addWorkspaceMember(Number(rows[0].workspace_id), member, workspaceRole);
    }

    const response = await this.api.post(`/api/projects/${projectId}/members`, {
      headers: bearer(actor),
      data: { userId: member.id, role },
    });
    expect(response.status(), "テスト用メンバーを追加できること").toBe(201);
  }

  async createTask(projectId: number, actor: TestUser, body: Partial<Omit<Task, "id" | "projectId">> = {}): Promise<Task> {
    const response = await this.api.post(`/api/projects/${projectId}/tasks`, {
      headers: bearer(actor),
      data: {
        title: unique("E2Eタスク"),
        description: "E2Eテスト用タスク",
        status: "TODO",
        priority: "MEDIUM",
        dueDate: "2026-10-31",
        ...body,
      },
    });
    expect(response.status(), "テスト用タスクを作成できること").toBe(201);
    return response.json();
  }

  async createComment(taskId: number, actor: TestUser, comment = unique("E2Eコメント")): Promise<CreatedComment> {
    const response = await this.api.post(`/api/tasks/${taskId}/comments`, {
      headers: bearer(actor),
      data: { comment },
    });
    expect(response.status(), "テスト用コメントを投稿できること").toBe(201);
    return response.json();
  }

  async cleanup(): Promise<void> {
    // 監査ログは actor_user_id を RESTRICT で参照するため、ユーザー削除前に消す(実行者/WS で絞る)
    await this.db.query(
      "DELETE FROM audit_logs WHERE actor_user_id = ANY($1::bigint[]) OR workspace_id = ANY($2::bigint[])",
      [this.userIds, this.workspaceIds],
    );
    // プロジェクトはワークスペースを RESTRICT で参照するため、ワークスペースより先に削除する
    await this.db.query(
      `DELETE FROM projects
        WHERE id = ANY($1::bigint[])
           OR id IN (SELECT project_id FROM project_members WHERE user_id = ANY($2::bigint[]))
           OR workspace_id = ANY($3::bigint[])`,
      [this.projectIds, this.userIds, this.workspaceIds],
    );
    // workspace_members / invitations は ON DELETE CASCADE で連動削除される
    await this.db.query("DELETE FROM workspaces WHERE id = ANY($1::bigint[])", [this.workspaceIds]);
    await this.db.query("DELETE FROM users WHERE id = ANY($1::bigint[])", [this.userIds]);
  }
}

export const test = base.extend<{ api: APIRequestContext; data: TestDataFactory }, { db: pg.Pool }>({
  db: [
    async ({}, use) => {
      const pool = new pg.Pool({ ...DB_CONFIG, max: 4 });
      await use(pool);
      await pool.end();
    },
    { scope: "worker" },
  ],

  api: async ({ playwright }, use) => {
    // BFFを経由しないAPIテストでも、BFFと同じ共有シークレットを付けてAPIの検証を通す(security-review-2.md SEC2-01)
    const context = await playwright.request.newContext({
      baseURL: API_URL,
      extraHTTPHeaders: { "X-Origin-Verify": ORIGIN_VERIFY_SECRET },
    });
    await use(context);
    await context.dispose();
  },

  data: async ({ api, db }, use) => {
    const factory = new TestDataFactory(api, db);
    await use(factory);
    await factory.cleanup();
  },
});

export { expect };
