import { createHmac } from "node:crypto";
import { bearer, expect, test } from "../../support/fixtures";

/** FakeGitProvider と同じ方式(HMAC-SHA256 の 16進小文字)で署名する。 */
function sign(body: string, secret: string): string {
  return createHmac("sha256", secret).update(body).digest("hex");
}

const SECRET = "wh-e2e-secret";

async function setupLinkedRepo(api: any, data: any, repoId: string) {
  const owner = await data.createUser("オーナー");
  const project = await data.createProject(owner);
  const conn = await (await api.post(`/api/workspaces/${project.workspaceId}/git-connections`, {
    headers: bearer(owner),
    data: { provider: "GITHUB", authType: "GITHUB_APP", externalAccount: `acme-${repoId}`, secretRef: SECRET },
  })).json();
  const linked = await api.post(`/api/projects/${project.id}/repository-links`, {
    headers: bearer(owner),
    data: { gitConnectionId: conn.id, externalRepoId: repoId, repoFullName: "acme/app" },
  });
  expect(linked.status()).toBe(201);
  return { owner, project };
}

function webhookBody(repoId: string): string {
  return JSON.stringify({
    type: "pr_merged",
    repoId,
    repoFullName: "acme/app",
    ref: "feature/1-x",
    title: "Closes #1",
  });
}

function postWebhook(api: any, body: string, signature: string | null, deliveryId: string | null) {
  const headers: Record<string, string> = { "content-type": "application/json" };
  if (signature !== null) headers["x-signature"] = signature;
  if (deliveryId !== null) headers["x-delivery-id"] = deliveryId;
  // Webhook は BFF 非経由・認証なし。署名で正当性を担保する
  return api.post("/api/git/webhooks/github", { headers, data: body });
}

test.describe("5.28 Git Webhook API (M4)", () => {
  test("API-3801 正しい署名の Webhook を受理する(202)", async ({ api, data }) => {
    const repoId = `r-${Date.now()}-a`;
    await setupLinkedRepo(api, data, repoId);
    const body = webhookBody(repoId);

    const res = await postWebhook(api, body, sign(body, SECRET), "d-1");
    expect(res.status()).toBe(202);
    expect((await res.json()).status).toBe("accepted");
  });

  test("API-3802 冪等: 同一配信IDの再送は200(duplicate)", async ({ api, data }) => {
    const repoId = `r-${Date.now()}-b`;
    await setupLinkedRepo(api, data, repoId);
    const body = webhookBody(repoId);
    const sig = sign(body, SECRET);

    expect((await postWebhook(api, body, sig, "d-dup")).status()).toBe(202);
    const again = await postWebhook(api, body, sig, "d-dup");
    expect(again.status()).toBe(200);
    expect((await again.json()).status).toBe("duplicate");
  });

  test("API-3803 署名が不正なら401", async ({ api, data }) => {
    const repoId = `r-${Date.now()}-c`;
    await setupLinkedRepo(api, data, repoId);
    const body = webhookBody(repoId);

    const res = await postWebhook(api, body, "deadbeef", "d-2");
    expect(res.status()).toBe(401);
  });

  test("API-3804 連携していないリポジトリは202(ignored)", async ({ api, data }) => {
    const repoId = `r-${Date.now()}-d`;
    await setupLinkedRepo(api, data, repoId);
    const body = webhookBody(`unlinked-${repoId}`);

    const res = await postWebhook(api, body, sign(body, SECRET), "d-3");
    expect(res.status()).toBe(202);
    expect((await res.json()).status).toBe("ignored");
  });

  test("API-3805 配信ID無しは400、未知プロバイダは404", async ({ api, data }) => {
    const repoId = `r-${Date.now()}-e`;
    await setupLinkedRepo(api, data, repoId);
    const body = webhookBody(repoId);
    const sig = sign(body, SECRET);

    expect((await postWebhook(api, body, sig, null)).status()).toBe(400);

    const unknown = await api.post("/api/git/webhooks/bitbucket", {
      headers: { "content-type": "application/json", "x-signature": sig, "x-delivery-id": "d-4" },
      data: body,
    });
    expect(unknown.status()).toBe(404);
  });
});
