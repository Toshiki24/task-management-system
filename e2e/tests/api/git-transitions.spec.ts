import { createHmac } from "node:crypto";
import { bearer, expect, expectValidationError, test } from "../../support/fixtures";

function sign(body: string, secret: string): string {
  return createHmac("sha256", secret).update(body).digest("hex");
}

const SECRET = "trans-e2e-secret";

test.describe("5.31 Git 自動遷移ルールAPI (M4)", () => {
  test("API-4101 WS ルールの置き換えは WS Admin のみ、取得はメンバーも可", async ({ api, data }) => {
    const owner = await data.createUser("オーナー");
    const member = await data.createUser("メンバー");
    const workspaceId = await data.createWorkspace(owner);
    await data.addWorkspaceMember(workspaceId, member, "MEMBER");
    const payload = { rules: [{ trigger: "PR_MERGED", toStatusKey: "DONE", enabled: true }] };

    const forbidden = await api.put(`/api/workspaces/${workspaceId}/transition-rules`, { headers: bearer(member), data: payload });
    expect(forbidden.status()).toBe(403);

    const ok = await api.put(`/api/workspaces/${workspaceId}/transition-rules`, { headers: bearer(owner), data: payload });
    expect(ok.status()).toBe(200);
    expect((await ok.json()).length).toBe(1);

    // 取得はメンバーも可
    const list = await api.get(`/api/workspaces/${workspaceId}/transition-rules`, { headers: bearer(member) });
    expect(list.status()).toBe(200);
  });

  test("API-4102 遷移先がワークフローに無いキーなら400", async ({ api, data }) => {
    const owner = await data.createUser("オーナー");
    const workspaceId = await data.createWorkspace(owner);

    const response = await api.put(`/api/workspaces/${workspaceId}/transition-rules`, {
      headers: bearer(owner),
      data: { rules: [{ trigger: "PR_MERGED", toStatusKey: "NOPE", enabled: true }] },
    });
    await expectValidationError(response, "toStatusKey");
  });

  test("API-4103 PR マージ webhook でルール＋identity があればタスクが完了へ遷移する", async ({ api, data }) => {
    const owner = await data.createUser("オーナー");
    const project = await data.createProject(owner);
    const repoId = `r-${Date.now()}`;
    const conn = await (await api.post(`/api/workspaces/${project.workspaceId}/git-connections`, {
      headers: bearer(owner),
      data: { provider: "GITHUB", authType: "GITHUB_APP", externalAccount: `acme-${repoId}`, secretRef: SECRET },
    })).json();
    await api.post(`/api/projects/${project.id}/repository-links`, {
      headers: bearer(owner),
      data: { gitConnectionId: conn.id, externalRepoId: repoId, repoFullName: "acme/app" },
    });
    // owner を Git ユーザー gh-1 に対応付け
    await api.post(`/api/workspaces/${project.workspaceId}/git-identities`, {
      headers: bearer(owner),
      data: { userId: owner.id, provider: "GITHUB", externalUserId: "gh-1", externalUsername: "octocat" },
    });
    // PR マージ → DONE
    await api.put(`/api/workspaces/${project.workspaceId}/transition-rules`, {
      headers: bearer(owner),
      data: { rules: [{ trigger: "PR_MERGED", toStatusKey: "DONE", enabled: true }] },
    });
    const task = await data.createTask(project.id, owner, { title: "遷移対象", status: "TODO" });

    const body = JSON.stringify({
      type: "pr_merged", repoId, repoFullName: "acme/app", ref: "42",
      title: `Closes #${task.id}`, actorId: "gh-1", actorName: "octocat",
    });
    const res = await api.post("/api/git/webhooks/github", {
      headers: { "content-type": "application/json", "x-signature": sign(body, SECRET), "x-delivery-id": "d-trans" },
      data: body,
    });
    expect(res.status()).toBe(202);

    const updated = await (await api.get(`/api/tasks/${task.id}`, { headers: bearer(owner) })).json();
    expect(updated.status).toBe("DONE");
  });

  test("API-4104 プロジェクト個別ルールが WS 既定を上書きする", async ({ api, data }) => {
    const owner = await data.createUser("オーナー");
    const project = await data.createProject(owner);
    await api.put(`/api/workspaces/${project.workspaceId}/transition-rules`, {
      headers: bearer(owner),
      data: { rules: [{ trigger: "PR_MERGED", toStatusKey: "DONE", enabled: true }] },
    });
    const put = await api.put(`/api/projects/${project.id}/transition-rules`, {
      headers: bearer(owner),
      data: { rules: [{ trigger: "PR_MERGED", toStatusKey: "IN_PROGRESS", enabled: true }] },
    });
    expect(put.status()).toBe(200);
    const list = await (await api.get(`/api/projects/${project.id}/transition-rules`, { headers: bearer(owner) })).json();
    expect(list[0].toStatusKey).toBe("IN_PROGRESS");
  });
});
