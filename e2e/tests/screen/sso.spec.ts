import { bearer, expect, test } from "../../support/fixtures";

test.describe("7.40 SCR-040 SSO ログイン導線 (M5)", () => {
  test("SCR-040-01 有効な SSO 接続がログイン画面にボタンとして出る", async ({ page, data, api }) => {
    const admin = await data.createUser("SSO管理者");
    await data.makeSystemAdmin(admin);

    const name = `E2E-SSO-${Date.now()}`;
    const create = await api.post("/api/system/sso-connections", {
      headers: bearer(admin),
      data: {
        name,
        issuer: "https://idp.invalid.example",
        clientId: "client-123",
        secretRef: "oidc-secret",
        scopes: "openid email profile",
        autoProvision: false,
        provisionWorkspaceId: null,
        provisionRole: null,
      },
    });
    expect(create.status()).toBe(201);
    const { id } = await create.json();

    await page.goto("/login");

    // 「＜名称＞ でログイン」ボタン(リンク)が表示され、開始URLを指す
    const ssoButton = page.getByRole("link", { name: `${name} でログイン` });
    await expect(ssoButton).toBeVisible();
    await expect(ssoButton).toHaveAttribute("href", `/api/bff/auth/sso/${id}/start`);
  });
});
