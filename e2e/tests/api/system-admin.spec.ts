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
});
