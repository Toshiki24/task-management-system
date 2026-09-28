import { createHmac } from "node:crypto";
import { readFileSync } from "node:fs";
import path from "node:path";
import { bearer, expect, test, type TestUser } from "../../support/fixtures";

/**
 * E2E用のAPIは Development 環境で起動するため、appsettings.Development.json の署名鍵を使う。
 * テストから任意の有効期限のJWTを作り、APIが期限切れのトークンをどう扱うかを確認する。
 */
function developmentJwtSettings(): { key: string; issuer: string; audience: string } {
  const apiDir = path.resolve(__dirname, "../../../backend/src/TaskManagementSystem.Api");
  const base = JSON.parse(readFileSync(path.join(apiDir, "appsettings.json"), "utf-8"));
  const development = JSON.parse(readFileSync(path.join(apiDir, "appsettings.Development.json"), "utf-8"));
  return { key: development.Jwt.Key, issuer: base.Jwt.Issuer, audience: base.Jwt.Audience };
}

function base64Url(value: string | Buffer): string {
  return Buffer.from(value).toString("base64url");
}

/** 有効期限(exp)を指定してJWTを作る(署名はAPIと同じ HS256) */
function createJwt(user: TestUser, expiresInSeconds: number): string {
  const { key, issuer, audience } = developmentJwtSettings();
  const now = Math.floor(Date.now() / 1000);
  const header = base64Url(JSON.stringify({ alg: "HS256", typ: "JWT" }));
  const payload = base64Url(
    JSON.stringify({
      sub: String(user.id),
      email: user.email,
      name: user.name,
      iss: issuer,
      aud: audience,
      iat: now - 600,
      nbf: now - 600,
      exp: now + expiresInSeconds,
    }),
  );
  const signature = createHmac("sha256", key).update(`${header}.${payload}`).digest("base64url");
  return `${header}.${payload}.${signature}`;
}

test.describe("9.11 SEC-07 JWTの有効期限の許容時間", () => {
  test("SEC-07-01 有効期限を60秒過ぎたアクセストークンは拒否される", async ({ api, data }) => {
    const user = await data.createUser();

    const response = await api.get("/api/projects", { headers: bearer({ ...user, token: createJwt(user, -60) }) });

    expect(response.status()).toBe(401);
  });

  test("SEC-07-02 自作したJWTの署名・内容がAPIと一致している（テストの前提確認）", async ({ api, data }) => {
    // SEC-07-01 が「署名が不正なため」ではなく「期限切れのため」に拒否されていることを確かめるため、
    // 同じ方法で作った有効期限内のトークンは受け付けられることを確認する
    const user = await data.createUser();

    const response = await api.get("/api/projects", { headers: bearer({ ...user, token: createJwt(user, 60) }) });

    expect(response.status()).toBe(200);
  });
});

test.describe("9.12 SEC-12 / SEC-13 メンバー削除時の整合性", () => {
  test("SEC-12-01 プロジェクトの最後のOWNERは削除できない", async ({ api, data, db }) => {
    const owner = await data.createUser("オーナー");
    const project = await data.createProject(owner);

    const response = await api.delete(`/api/projects/${project.id}/members/${owner.id}`, { headers: bearer(owner) });

    expect(response.status()).toBe(409);
    expect(await response.json()).toEqual({ message: "プロジェクトには少なくとも1人のOWNERが必要です。" });
    const { rowCount } = await db.query(
      "SELECT 1 FROM project_members WHERE project_id = $1 AND user_id = $2",
      [project.id, owner.id],
    );
    expect(rowCount).toBe(1);
  });

  test("SEC-12-02 OWNERが複数いれば、OWNERを削除できる", async ({ api, data }) => {
    const owner = await data.createUser("オーナー");
    const secondOwner = await data.createUser("2人目のオーナー");
    const project = await data.createProject(owner);
    await data.addMember(project.id, owner, secondOwner, "OWNER");

    const response = await api.delete(`/api/projects/${project.id}/members/${owner.id}`, { headers: bearer(owner) });

    expect(response.status()).toBe(204);
  });

  test("SEC-13-01 メンバーを削除すると、そのユーザーが担当していたタスクは未割り当てになる", async ({ api, data, db }) => {
    const owner = await data.createUser("オーナー");
    const member = await data.createUser("メンバー");
    const project = await data.createProject(owner);
    await data.addMember(project.id, owner, member);
    const task = await data.createTask(project.id, owner, { assigneeId: member.id });
    // 別のプロジェクトで同じユーザーが担当しているタスクは影響を受けない
    const otherProject = await data.createProject(owner);
    await data.addMember(otherProject.id, owner, member);
    const otherTask = await data.createTask(otherProject.id, owner, { assigneeId: member.id });

    const response = await api.delete(`/api/projects/${project.id}/members/${member.id}`, { headers: bearer(owner) });

    expect(response.status()).toBe(204);
    const { rows } = await db.query<{ id: string; assignee_id: string | null }>(
      "SELECT id, assignee_id FROM tasks WHERE id = ANY($1::bigint[])",
      [[task.id, otherTask.id]],
    );
    const assignees = Object.fromEntries(rows.map((r) => [Number(r.id), r.assignee_id === null ? null : Number(r.assignee_id)]));
    expect(assignees[task.id]).toBeNull();
    expect(assignees[otherTask.id]).toBe(member.id);
  });
});

test.describe("9.13 SEC-08 HSTS", () => {
  test("SEC-08-01 画面のレスポンスに Strict-Transport-Security が付く（本番ビルド）", async ({ page }) => {
    const response = await page.request.get("/login");

    expect(response.headers()["strict-transport-security"]).toBe("max-age=31536000; includeSubDomains");
  });
});
