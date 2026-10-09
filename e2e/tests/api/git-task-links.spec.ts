import { createHmac } from "node:crypto";
import { bearer, expect, test } from "../../support/fixtures";

function sign(body: string, secret: string): string {
  return createHmac("sha256", secret).update(body).digest("hex");
}

const SECRET = "link-e2e-secret";

/** 連携済みリポジトリ・タスクを用意し、webhook を送れる文脈を返す。 */
async function setup(api: any, data: any, repoId: string) {
  const owner = await data.createUser("オーナー");
  const project = await data.createProject(owner);
  const conn = await (await api.post(`/api/workspaces/${project.workspaceId}/git-connections`, {
    headers: bearer(owner),
    data: { provider: "GITHUB", authType: "GITHUB_APP", externalAccount: `acme-${repoId}`, secretRef: SECRET },
  })).json();
  await api.post(`/api/projects/${project.id}/repository-links`, {
    headers: bearer(owner),
    data: { gitConnectionId: conn.id, externalRepoId: repoId, repoFullName: "acme/app" },
  });
  const task = await data.createTask(project.id, owner, { title: "連携対象タスク" });
  return { owner, project, task };
}

function prMergedBody(repoId: string, taskId: number, ref = "42") {
  return JSON.stringify({
    type: "pr_merged",
    repoId,
    repoFullName: "acme/app",
    ref,
    title: `Closes #${taskId}`,
    url: "https://example.test/acme/app/pull/42",
  });
}

function postWebhook(api: any, body: string, deliveryId: string) {
  return api.post("/api/git/webhooks/github", {
    headers: { "content-type": "application/json", "x-signature": sign(body, SECRET), "x-delivery-id": deliveryId },
    data: body,
  });
}

test.describe("5.30 Git 双方向リンク(取り込み)API (M4)", () => {
  test("API-4001 PR マージ webhook でタスクに Git リンクが作成される", async ({ api, data }) => {
    const repoId = `r-${Date.now()}-a`;
    const { owner, task } = await setup(api, data, repoId);

    expect((await postWebhook(api, prMergedBody(repoId, task.id), "d-1")).status()).toBe(202);

    const links = await (await api.get(`/api/tasks/${task.id}/git/links`, { headers: bearer(owner) })).json();
    expect(links.length).toBe(1);
    expect(links[0]).toMatchObject({ linkType: "PR", state: "MERGED", externalRef: "42" });
  });

  test("API-4002 同一 PR の再送はリンクを重複させない(冪等)", async ({ api, data }) => {
    const repoId = `r-${Date.now()}-b`;
    const { owner, task } = await setup(api, data, repoId);
    const body = prMergedBody(repoId, task.id);

    expect((await postWebhook(api, body, "d-same")).status()).toBe(202);
    // 同一配信IDの再送は duplicate(200)
    expect((await postWebhook(api, body, "d-same")).status()).toBe(200);

    const links = await (await api.get(`/api/tasks/${task.id}/git/links`, { headers: bearer(owner) })).json();
    expect(links.length).toBe(1);
  });

  test("API-4003 非所属ユーザーには Git リンク一覧が404", async ({ api, data }) => {
    const repoId = `r-${Date.now()}-c`;
    const { task } = await setup(api, data, repoId);
    const outsider = await data.createUser("部外者");

    const response = await api.get(`/api/tasks/${task.id}/git/links`, { headers: bearer(outsider) });
    expect(response.status()).toBe(404);
  });
});
