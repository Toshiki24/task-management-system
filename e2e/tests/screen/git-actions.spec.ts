import { bearer, expect, test } from "../../support/fixtures";
import { signIn } from "../../support/ui";

test.describe("7.33 SCR-033 タスクからの Git 操作 (M4)", () => {
  test("SCR-033-01 タスク詳細からブランチを作成するとリンクが表示される", async ({ page, api, data }) => {
    const owner = await data.createUser("オーナー");
    const project = await data.createProject(owner);
    const conn = await (await api.post(`/api/workspaces/${project.workspaceId}/git-connections`, {
      headers: bearer(owner),
      data: { provider: "GITHUB", authType: "GITHUB_APP", externalAccount: `acme-${Date.now()}` },
    })).json();
    await api.post(`/api/projects/${project.id}/repository-links`, {
      headers: bearer(owner),
      data: { gitConnectionId: conn.id, externalRepoId: `r-${Date.now()}`, repoFullName: "acme/app", defaultBranch: "main" },
    });
    const task = await data.createTask(project.id, owner, { title: "Fix login bug" });

    await signIn(page, owner);
    await page.goto(`/tasks/${task.id}`);

    const section = page.locator("div").filter({ has: page.getByRole("heading", { name: "Git 連携" }) }).last();
    await section.getByRole("button", { name: "ブランチ作成" }).click();

    await expect(section.getByText(`feature/${task.id}-fix-login-bug`)).toBeVisible();
  });
});
