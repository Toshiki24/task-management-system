import { createHmac } from "node:crypto";
import { bearer, expect, test } from "../../support/fixtures";
import { signIn } from "../../support/ui";

const SECRET = "link-scr-secret";

test.describe("7.31 SCR-031 タスクの Git 連携表示 (M4)", () => {
  test("SCR-031-01 PR マージ後、タスク詳細に Git リンクが表示される", async ({ page, api, data }) => {
    const owner = await data.createUser("オーナー");
    const project = await data.createProject(owner);
    const repoId = `r-scr-${Date.now()}`;
    const conn = await (await api.post(`/api/workspaces/${project.workspaceId}/git-connections`, {
      headers: bearer(owner),
      data: { provider: "GITHUB", authType: "GITHUB_APP", externalAccount: `acme-${repoId}`, secretRef: SECRET },
    })).json();
    await api.post(`/api/projects/${project.id}/repository-links`, {
      headers: bearer(owner),
      data: { gitConnectionId: conn.id, externalRepoId: repoId, repoFullName: "acme/app" },
    });
    const task = await data.createTask(project.id, owner, { title: "連携対象タスク" });

    const body = JSON.stringify({
      type: "pr_merged", repoId, repoFullName: "acme/app", ref: "42",
      title: `Closes #${task.id}`, url: "https://example.test/acme/app/pull/42",
    });
    const sig = createHmac("sha256", SECRET).update(body).digest("hex");
    const res = await api.post("/api/git/webhooks/github", {
      headers: { "content-type": "application/json", "x-signature": sig, "x-delivery-id": `d-scr-${Date.now()}` },
      data: body,
    });
    expect(res.status()).toBe(202);

    await signIn(page, owner);
    await page.goto(`/tasks/${task.id}`);

    const section = page.locator("div").filter({ has: page.getByRole("heading", { name: "Git 連携" }) }).last();
    await expect(section.getByText("プルリクエスト")).toBeVisible();
    await expect(section.getByText("マージ済み")).toBeVisible();
  });
});
