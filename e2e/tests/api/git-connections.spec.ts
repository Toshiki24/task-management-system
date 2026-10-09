import { bearer, expect, test } from "../../support/fixtures";

test.describe("5.26 Git接続API (M4)", () => {
  test("API-3601 作成は WS Admin のみ(メンバーは403)、一覧はメンバーも参照可", async ({ api, data }) => {
    const owner = await data.createUser("オーナー");
    const member = await data.createUser("メンバー");
    const workspaceId = await data.createWorkspace(owner);
    await data.addWorkspaceMember(workspaceId, member, "MEMBER");

    const forbidden = await api.post(`/api/workspaces/${workspaceId}/git-connections`, {
      headers: bearer(member),
      data: { provider: "GITHUB", authType: "GITHUB_APP", externalAccount: "acme" },
    });
    expect(forbidden.status()).toBe(403);

    const created = await api.post(`/api/workspaces/${workspaceId}/git-connections`, {
      headers: bearer(owner),
      data: { provider: "GITHUB", authType: "GITHUB_APP", externalAccount: "acme", secretRef: "tms/git/acme" },
    });
    expect(created.status()).toBe(201);

    // 一覧はメンバーも参照できる
    const list = await api.get(`/api/workspaces/${workspaceId}/git-connections`, { headers: bearer(member) });
    expect(list.status()).toBe(200);
    expect((await list.json()).length).toBe(1);
  });

  test("API-3602 資格情報(secretRef)はレスポンスに含まれない", async ({ api, data }) => {
    const owner = await data.createUser("オーナー");
    const workspaceId = await data.createWorkspace(owner);

    const created = await api.post(`/api/workspaces/${workspaceId}/git-connections`, {
      headers: bearer(owner),
      data: { provider: "GITHUB", authType: "PAT", externalAccount: "acme", secretRef: "tms/git/secret" },
    });
    const body = await created.json();
    expect(JSON.stringify(body)).not.toContain("secret");
    expect(body.secretRef).toBeUndefined();
  });

  test("API-3603 同一プロバイダ・アカウントの重複は409", async ({ api, data }) => {
    const owner = await data.createUser("オーナー");
    const workspaceId = await data.createWorkspace(owner);
    const payload = { provider: "GITHUB", authType: "GITHUB_APP", externalAccount: "dup" };

    expect((await api.post(`/api/workspaces/${workspaceId}/git-connections`, { headers: bearer(owner), data: payload })).status()).toBe(201);
    const dup = await api.post(`/api/workspaces/${workspaceId}/git-connections`, { headers: bearer(owner), data: payload });
    expect(dup.status()).toBe(409);
  });

  test("API-3604 更新・削除ができる", async ({ api, data }) => {
    const owner = await data.createUser("オーナー");
    const workspaceId = await data.createWorkspace(owner);
    const created = await (await api.post(`/api/workspaces/${workspaceId}/git-connections`, {
      headers: bearer(owner),
      data: { provider: "GITLAB", authType: "GROUP_TOKEN", externalAccount: "grp" },
    })).json();

    const updated = await api.patch(`/api/git-connections/${created.id}`, {
      headers: bearer(owner),
      data: { authType: "OAUTH", externalAccount: "grp", status: "DISABLED" },
    });
    expect(updated.status()).toBe(200);
    expect((await updated.json()).status).toBe("DISABLED");

    const del = await api.delete(`/api/git-connections/${created.id}`, { headers: bearer(owner) });
    expect(del.status()).toBe(204);
  });

  test("API-3605 非所属ユーザーには404(存在を開示しない)", async ({ api, data }) => {
    const owner = await data.createUser("オーナー");
    const outsider = await data.createUser("部外者");
    const workspaceId = await data.createWorkspace(owner);

    const response = await api.get(`/api/workspaces/${workspaceId}/git-connections`, { headers: bearer(outsider) });
    expect(response.status()).toBe(404);
  });
});
