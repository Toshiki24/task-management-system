import { bearer, expect, expectValidationError, test } from "../../support/fixtures";

async function createConnection(api: any, workspaceId: number, owner: any, account = "acme") {
  const res = await api.post(`/api/workspaces/${workspaceId}/git-connections`, {
    headers: bearer(owner),
    data: { provider: "GITHUB", authType: "GITHUB_APP", externalAccount: account },
  });
  expect(res.status()).toBe(201);
  return res.json();
}

test.describe("5.27 リポジトリ連携API (M4)", () => {
  test("API-3701 連携の作成は OWNER/WS Admin のみ、一般メンバーは403", async ({ api, data }) => {
    const owner = await data.createUser("オーナー");
    const member = await data.createUser("メンバー");
    const project = await data.createProject(owner);
    await data.addMember(project.id, owner, member);
    const conn = await createConnection(api, project.workspaceId, owner);
    const payload = { gitConnectionId: conn.id, externalRepoId: "r1", repoFullName: "acme/app", defaultBranch: "main" };

    const forbidden = await api.post(`/api/projects/${project.id}/repository-links`, { headers: bearer(member), data: payload });
    expect(forbidden.status()).toBe(403);

    const created = await api.post(`/api/projects/${project.id}/repository-links`, { headers: bearer(owner), data: payload });
    expect(created.status()).toBe(201);
    expect((await created.json()).repoFullName).toBe("acme/app");
  });

  test("API-3702 別ワークスペースの接続は400", async ({ api, data }) => {
    const owner = await data.createUser("オーナー");
    const project = await data.createProject(owner);
    const otherWorkspaceId = await data.createWorkspace(owner);
    const foreignConn = await createConnection(api, otherWorkspaceId, owner, "other");

    const response = await api.post(`/api/projects/${project.id}/repository-links`, {
      headers: bearer(owner),
      data: { gitConnectionId: foreignConn.id, externalRepoId: "r1", repoFullName: "other/app" },
    });
    await expectValidationError(response, "gitConnectionId");
  });

  test("API-3703 同じ接続・同じリポジトリの重複連携は409", async ({ api, data }) => {
    const owner = await data.createUser("オーナー");
    const project = await data.createProject(owner);
    const conn = await createConnection(api, project.workspaceId, owner);
    const payload = { gitConnectionId: conn.id, externalRepoId: "r1", repoFullName: "acme/app" };

    expect((await api.post(`/api/projects/${project.id}/repository-links`, { headers: bearer(owner), data: payload })).status()).toBe(201);
    const dup = await api.post(`/api/projects/${project.id}/repository-links`, { headers: bearer(owner), data: payload });
    expect(dup.status()).toBe(409);
  });

  test("API-3704 削除ができ、非所属ユーザーには一覧が404", async ({ api, data }) => {
    const owner = await data.createUser("オーナー");
    const outsider = await data.createUser("部外者");
    const project = await data.createProject(owner);
    const conn = await createConnection(api, project.workspaceId, owner);
    const link = await (await api.post(`/api/projects/${project.id}/repository-links`, {
      headers: bearer(owner),
      data: { gitConnectionId: conn.id, externalRepoId: "r1", repoFullName: "acme/app" },
    })).json();

    const del = await api.delete(`/api/repository-links/${link.id}`, { headers: bearer(owner) });
    expect(del.status()).toBe(204);

    const forbidden = await api.get(`/api/projects/${project.id}/repository-links`, { headers: bearer(outsider) });
    expect(forbidden.status()).toBe(404);
  });
});
