import { bearer, expect, expectValidationError, test } from "../../support/fixtures";

test.describe("5.29 Git identity マッピングAPI (M4)", () => {
  test("API-3901 作成・一覧は WS Admin のみ(メンバーは403/404)", async ({ api, data }) => {
    const owner = await data.createUser("オーナー");
    const member = await data.createUser("メンバー");
    const workspaceId = await data.createWorkspace(owner);
    await data.addWorkspaceMember(workspaceId, member, "MEMBER");

    const forbidden = await api.post(`/api/workspaces/${workspaceId}/git-identities`, {
      headers: bearer(member),
      data: { userId: member.id, provider: "GITHUB", externalUserId: "u-1", externalUsername: "octocat" },
    });
    expect(forbidden.status()).toBe(403);

    const created = await api.post(`/api/workspaces/${workspaceId}/git-identities`, {
      headers: bearer(owner),
      data: { userId: member.id, provider: "GITHUB", externalUserId: "u-1", externalUsername: "octocat" },
    });
    expect(created.status()).toBe(201);
    expect((await created.json()).userId).toBe(member.id);

    // 一般メンバーには一覧を開示しない(Admin 限定)
    expect((await api.get(`/api/workspaces/${workspaceId}/git-identities`, { headers: bearer(member) })).status()).toBe(404);
    const list = await api.get(`/api/workspaces/${workspaceId}/git-identities`, { headers: bearer(owner) });
    expect((await list.json()).length).toBe(1);
  });

  test("API-3902 ワークスペース非メンバーへの対応付けは400", async ({ api, data }) => {
    const owner = await data.createUser("オーナー");
    const outsider = await data.createUser("部外者");
    const workspaceId = await data.createWorkspace(owner);

    const response = await api.post(`/api/workspaces/${workspaceId}/git-identities`, {
      headers: bearer(owner),
      data: { userId: outsider.id, provider: "GITHUB", externalUserId: "u-2" },
    });
    await expectValidationError(response, "userId");
  });

  test("API-3903 同一プロバイダ・外部ユーザーIDの重複は409", async ({ api, data }) => {
    const owner = await data.createUser("オーナー");
    const workspaceId = await data.createWorkspace(owner);
    const payload = { userId: owner.id, provider: "GITHUB", externalUserId: "dup-1" };

    expect((await api.post(`/api/workspaces/${workspaceId}/git-identities`, { headers: bearer(owner), data: payload })).status()).toBe(201);
    const dup = await api.post(`/api/workspaces/${workspaceId}/git-identities`, { headers: bearer(owner), data: payload });
    expect(dup.status()).toBe(409);
  });

  test("API-3904 削除ができ、非所属ユーザーには一覧が404", async ({ api, data }) => {
    const owner = await data.createUser("オーナー");
    const outsider = await data.createUser("部外者");
    const workspaceId = await data.createWorkspace(owner);
    const created = await (await api.post(`/api/workspaces/${workspaceId}/git-identities`, {
      headers: bearer(owner),
      data: { userId: owner.id, provider: "GITLAB", externalUserId: "u-9" },
    })).json();

    const del = await api.delete(`/api/git-identities/${created.id}`, { headers: bearer(owner) });
    expect(del.status()).toBe(204);

    expect((await api.get(`/api/workspaces/${workspaceId}/git-identities`, { headers: bearer(outsider) })).status()).toBe(404);
  });
});
