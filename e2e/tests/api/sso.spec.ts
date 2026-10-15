import { bearer, expect, test } from "../../support/fixtures";

/** SSO 接続を1件作成して id を返す。 */
async function createConnection(
  api: import("@playwright/test").APIRequestContext,
  admin: { token: string },
  overrides: Record<string, unknown> = {},
) {
  const res = await api.post("/api/system/sso-connections", {
    headers: bearer(admin as never),
    data: {
      name: `IdP-${Date.now()}-${Math.floor(Math.random() * 1000)}`,
      issuer: "https://idp.invalid.example",
      clientId: "client-123",
      secretRef: "oidc-secret",
      scopes: "openid email profile",
      autoProvision: false,
      provisionWorkspaceId: null,
      provisionRole: null,
      ...overrides,
    },
  });
  expect(res.status()).toBe(201);
  return res.json();
}

test.describe("5.40 SSO(OIDC)API (M5)", () => {
  test("API-5001 System Admin は SSO 接続を作成・一覧・削除できる", async ({ data, api }) => {
    const admin = await data.createUser("SSO管理者");
    await data.makeSystemAdmin(admin);

    const created = await createConnection(api, admin, { name: `Google-${Date.now()}` });
    expect(created.hasSecret).toBe(true); // 値は返さず、設定有無のみ
    expect(created).not.toHaveProperty("secretRef");

    const list = await (await api.get("/api/system/sso-connections", { headers: bearer(admin) })).json();
    expect(list.some((c: { id: number }) => c.id === created.id)).toBe(true);

    const del = await api.delete(`/api/system/sso-connections/${created.id}`, { headers: bearer(admin) });
    expect(del.status()).toBe(204);
  });

  test("API-5002 System Admin 以外は SSO 接続を管理できない(403)", async ({ data, api }) => {
    const user = await data.createUser("一般");
    const res = await api.get("/api/system/sso-connections", { headers: bearer(user) });
    expect(res.status()).toBe(403);
  });

  test("API-5003 有効な接続のみ公開一覧(/auth/sso/connections)に出る", async ({ data, api }) => {
    const admin = await data.createUser("SSO管理者");
    await data.makeSystemAdmin(admin);

    const enabled = await createConnection(api, admin, { name: `Enabled-${Date.now()}` });
    const disabled = await createConnection(api, admin, { name: `Disabled-${Date.now()}` });
    // 1件を無効化する
    await api.put(`/api/system/sso-connections/${disabled.id}`, {
      headers: bearer(admin),
      data: {
        name: disabled.name, issuer: disabled.issuer, clientId: disabled.clientId, secretRef: null,
        scopes: disabled.scopes, autoProvision: false, provisionWorkspaceId: null, provisionRole: null,
        isEnabled: false,
      },
    });

    const publicList = await (await api.get("/api/auth/sso/connections")).json();
    expect(publicList.some((c: { id: number }) => c.id === enabled.id)).toBe(true);
    expect(publicList.some((c: { id: number }) => c.id === disabled.id)).toBe(false);
    // 公開一覧は id と name のみ
    const row = publicList.find((c: { id: number }) => c.id === enabled.id);
    expect(Object.keys(row).sort()).toEqual(["id", "name"]);
  });

  test("API-5004 開始はディスカバリ失敗時 502、コールバックの不正 state は 401", async ({ data, api }) => {
    const admin = await data.createUser("SSO管理者");
    await data.makeSystemAdmin(admin);
    const connection = await createConnection(api, admin);

    // issuer が到達不能なため、ディスカバリに失敗して 502
    const redirectUri = encodeURIComponent("http://localhost:3100/api/bff/auth/sso/callback");
    const start = await api.get(`/api/auth/sso/${connection.id}/start?redirectUri=${redirectUri}`);
    expect(start.status()).toBe(502);

    // 不明な state のコールバックは 401
    const callback = await api.post("/api/auth/sso/callback", {
      data: { state: "no-such-state", code: "dummy" },
    });
    expect(callback.status()).toBe(401);
  });

  test("API-5005 存在しない接続の開始は 404", async ({ api }) => {
    const res = await api.get("/api/auth/sso/999999999/start?redirectUri=http%3A%2F%2Flocalhost%2Fcb");
    expect(res.status()).toBe(404);
  });
});
