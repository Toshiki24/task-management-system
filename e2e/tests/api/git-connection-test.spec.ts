import { bearer, expect, test } from "../../support/fixtures";

test.describe("5.33 Git接続 疎通確認API (M4)", () => {
  test("API-4301 WS Admin は疎通確認でき、状態が返る", async ({ api, data }) => {
    const owner = await data.createUser("オーナー");
    const workspaceId = await data.createWorkspace(owner);
    const conn = await (await api.post(`/api/workspaces/${workspaceId}/git-connections`, {
      headers: bearer(owner),
      data: { provider: "GITHUB", authType: "GITHUB_APP", externalAccount: "acme", secretRef: "s" },
    })).json();

    const res = await api.post(`/api/git-connections/${conn.id}/test`, { headers: bearer(owner) });
    expect(res.status()).toBe(200);
    // Fake プロバイダは一覧を返すため ACTIVE
    expect((await res.json()).status).toBe("ACTIVE");
  });

  test("API-4302 一般メンバーは疎通確認できない(403)", async ({ api, data }) => {
    const owner = await data.createUser("オーナー");
    const member = await data.createUser("メンバー");
    const workspaceId = await data.createWorkspace(owner);
    await data.addWorkspaceMember(workspaceId, member, "MEMBER");
    const conn = await (await api.post(`/api/workspaces/${workspaceId}/git-connections`, {
      headers: bearer(owner),
      data: { provider: "GITHUB", authType: "GITHUB_APP", externalAccount: "acme" },
    })).json();

    const res = await api.post(`/api/git-connections/${conn.id}/test`, { headers: bearer(member) });
    expect(res.status()).toBe(403);
  });
});
