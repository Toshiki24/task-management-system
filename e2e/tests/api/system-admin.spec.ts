import { bearer, expect, test } from "../../support/fixtures";

test.describe("5.38 管理コンソールAPI (M5)", () => {
  test("API-4801 System Admin は統計・全WS・全ユーザー・監査ログを取得できる", async ({ data, api }) => {
    const admin = await data.createUser("管理者");
    await data.makeSystemAdmin(admin);
    // 統計に反映される実データを用意する
    const workspaceId = await data.createWorkspace(admin, "ADMIN");
    await data.createProject(admin, { workspaceId });

    const stats = await (await api.get("/api/system/stats", { headers: bearer(admin) })).json();
    expect(stats.workspaceCount).toBeGreaterThanOrEqual(1);
    expect(stats.projectCount).toBeGreaterThanOrEqual(1);
    expect(stats.userCount).toBeGreaterThanOrEqual(1);
    expect(stats.systemAdminCount).toBeGreaterThanOrEqual(1);

    const workspaces = await (await api.get("/api/system/workspaces", { headers: bearer(admin) })).json();
    expect(workspaces.some((w: { id: number }) => w.id === workspaceId)).toBe(true);

    const users = await (await api.get("/api/system/users", { headers: bearer(admin) })).json();
    expect(users.some((u: { id: number; isSystemAdmin: boolean }) => u.id === admin.id && u.isSystemAdmin)).toBe(true);

    const logs = await (await api.get("/api/system/audit-logs?limit=10", { headers: bearer(admin) })).json();
    expect(Array.isArray(logs.items)).toBe(true);
    expect(typeof logs.total).toBe("number");
  });

  test("API-4802 System Admin 以外は管理APIにアクセスできない(403)", async ({ data, api }) => {
    const user = await data.createUser("一般");

    for (const path of ["/api/system/stats", "/api/system/workspaces", "/api/system/users", "/api/system/audit-logs"]) {
      const res = await api.get(path, { headers: bearer(user) });
      expect(res.status(), `${path} は 403`).toBe(403);
    }
  });

  test("API-4803 System Admin 権限の付与/剥奪が一覧と監査ログに反映される", async ({ data, api }) => {
    const admin = await data.createUser("管理者");
    await data.makeSystemAdmin(admin);
    const target = await data.createUser("対象");

    // 付与
    const grant = await api.post(`/api/system/admins/${target.id}`, { headers: bearer(admin) });
    expect(grant.status()).toBe(204);
    let users = await (await api.get("/api/system/users", { headers: bearer(admin) })).json();
    expect(users.find((u: { id: number }) => u.id === target.id).isSystemAdmin).toBe(true);

    // 付与が監査ログに記録される(アクションでフィルタ)
    const grantedLogs = await (
      await api.get("/api/system/audit-logs?action=user.system_admin.granted", { headers: bearer(admin) })
    ).json();
    expect(grantedLogs.items.some((l: { targetId: number }) => l.targetId === target.id)).toBe(true);

    // 剥奪
    const revoke = await api.delete(`/api/system/admins/${target.id}`, { headers: bearer(admin) });
    expect(revoke.status()).toBe(204);
    users = await (await api.get("/api/system/users", { headers: bearer(admin) })).json();
    expect(users.find((u: { id: number }) => u.id === target.id).isSystemAdmin).toBe(false);
  });

  test("API-4804 System Admin はメール/パスワードでユーザーを作成し、WSに追加できる(作成後ログイン可能)", async ({ data, api }) => {
    const admin = await data.createUser("管理者");
    await data.makeSystemAdmin(admin);
    const workspaceId = await data.createWorkspace(admin, "ADMIN");

    const email = `manual-${Date.now()}@example.test`;
    const create = await api.post("/api/system/users", {
      headers: bearer(admin),
      data: { email, password: "manual-password", name: null, workspaceId, role: "MEMBER" },
    });
    expect(create.status()).toBe(201);
    const created = await create.json();
    expect(created.email).toBe(email);

    // 作成したユーザーで実際にログインできる
    const login = await api.post("/api/auth/login", { data: { email, password: "manual-password" } });
    expect(login.status()).toBe(200);

    // 全ユーザー一覧に現れ、指定WSのメンバーになっている
    const users = await (await api.get("/api/system/users", { headers: bearer(admin) })).json();
    expect(users.some((u: { email: string }) => u.email === email)).toBe(true);
    const workspaces = await (await api.get("/api/system/workspaces", { headers: bearer(admin) })).json();
    expect(workspaces.find((w: { id: number }) => w.id === workspaceId).memberCount).toBeGreaterThanOrEqual(2);
  });

  test("API-4805 System Admin 以外はユーザーを作成できない(403)", async ({ data, api }) => {
    const user = await data.createUser("一般");
    const workspaceId = await data.createWorkspace(user, "ADMIN");

    const res = await api.post("/api/system/users", {
      headers: bearer(user),
      data: { email: `x-${Date.now()}@example.test`, password: "password12", workspaceId, role: "MEMBER" },
    });
    expect(res.status()).toBe(403);
  });

  test("API-4806 既存メールは409、存在しないWSは404", async ({ data, api }) => {
    const admin = await data.createUser("管理者");
    await data.makeSystemAdmin(admin);
    const workspaceId = await data.createWorkspace(admin, "ADMIN");

    // 既存メール(管理者自身のメール)で重複 → 409
    const dup = await api.post("/api/system/users", {
      headers: bearer(admin),
      data: { email: admin.email, password: "password12", workspaceId, role: "MEMBER" },
    });
    expect(dup.status()).toBe(409);

    // 存在しないワークスペース → 404
    const noWs = await api.post("/api/system/users", {
      headers: bearer(admin),
      data: { email: `new-${Date.now()}@example.test`, password: "password12", workspaceId: 999999999, role: "MEMBER" },
    });
    expect(noWs.status()).toBe(404);
  });
});
