import { bearer, expect, expectValidationError, test } from "../../support/fixtures";

async function setup(api: any, data: any) {
  const owner = await data.createUser("オーナー");
  const project = await data.createProject(owner);
  const conn = await (await api.post(`/api/workspaces/${project.workspaceId}/git-connections`, {
    headers: bearer(owner),
    data: { provider: "GITHUB", authType: "GITHUB_APP", externalAccount: `acme-${Date.now()}-${Math.random()}` },
  })).json();
  const link = await (await api.post(`/api/projects/${project.id}/repository-links`, {
    headers: bearer(owner),
    data: { gitConnectionId: conn.id, externalRepoId: `r-${Date.now()}-${Math.random()}`, repoFullName: "acme/app", defaultBranch: "main" },
  })).json();
  const task = await data.createTask(project.id, owner, { title: "Fix login bug" });
  return { owner, project, link, task };
}

test.describe("5.32 Git 能動操作API (M4)", () => {
  test("API-4201 タスクからブランチを作成できる(リンクが記録される)", async ({ api, data }) => {
    const { owner, task, link } = await setup(api, data);

    const res = await api.post(`/api/tasks/${task.id}/git/branch`, {
      headers: bearer(owner),
      data: { repositoryLinkId: link.id },
    });
    expect(res.status()).toBe(201);
    const body = await res.json();
    expect(body.linkType).toBe("BRANCH");
    expect(body.externalRef).toBe(`feature/${task.id}-fix-login-bug`);

    const links = await (await api.get(`/api/tasks/${task.id}/git/links`, { headers: bearer(owner) })).json();
    expect(links.length).toBe(1);
  });

  test("API-4202 タスクから PR を作成できる(OPEN のリンク)", async ({ api, data }) => {
    const { owner, task, link } = await setup(api, data);

    const res = await api.post(`/api/tasks/${task.id}/git/pull-request`, {
      headers: bearer(owner),
      data: { repositoryLinkId: link.id, sourceBranch: "feature/x" },
    });
    expect(res.status()).toBe(201);
    const body = await res.json();
    expect(body.linkType).toBe("PR");
    expect(body.state).toBe("OPEN");
  });

  test("API-4203 別プロジェクトの連携リポジトリは400", async ({ api, data }) => {
    const { owner, task } = await setup(api, data);
    const other = await setup(api, data);

    const res = await api.post(`/api/tasks/${task.id}/git/branch`, {
      headers: bearer(owner),
      data: { repositoryLinkId: other.link.id },
    });
    await expectValidationError(res, "repositoryLinkId");
  });

  test("API-4204 非所属ユーザーには 404(存在を開示しない)", async ({ api, data }) => {
    const { task, link } = await setup(api, data);
    const outsider = await data.createUser("部外者");

    const res = await api.post(`/api/tasks/${task.id}/git/branch`, {
      headers: bearer(outsider),
      data: { repositoryLinkId: link.id },
    });
    expect(res.status()).toBe(404);
  });
});
