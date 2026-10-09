import { bearer, expect, test } from "../../support/fixtures";
import { signIn } from "../../support/ui";

test.describe("7.29 SCR-029 Git 連携設定 (M4)", () => {
  test("SCR-029-01 ワークスペース設定で Git 接続を追加できる", async ({ page, data }) => {
    const owner = await data.createUser("オーナー");
    const workspaceId = await data.createWorkspace(owner);
    await signIn(page, owner);
    await page.goto(`/workspaces/${workspaceId}`);

    await page.getByRole("button", { name: "Git 接続を追加" }).click();
    await page.getByLabel("アカウント/組織/グループ").fill("acme");
    await page.getByRole("button", { name: "追加", exact: true }).click();

    await expect(page.getByText("acme")).toBeVisible();
  });

  test("SCR-029-02 プロジェクト設定でリポジトリを連携できる", async ({ page, api, data }) => {
    const owner = await data.createUser("オーナー");
    const project = await data.createProject(owner);
    // 連携先の接続を用意する
    await api.post(`/api/workspaces/${project.workspaceId}/git-connections`, {
      headers: bearer(owner),
      data: { provider: "GITHUB", authType: "GITHUB_APP", externalAccount: "acme" },
    });

    await signIn(page, owner);
    await page.goto(`/projects/${project.id}`);

    await page.getByRole("button", { name: "リポジトリを連携" }).click();
    await page.getByLabel("接続").selectOption({ label: "GitHub / acme" });
    await page.getByLabel("リポジトリ名(owner/repo)").fill("acme/app");
    await page.getByLabel("リポジトリID(プロバイダ側の安定ID)").fill("123456");
    await page.getByRole("button", { name: "連携", exact: true }).click();

    await expect(page.getByText("acme/app")).toBeVisible();
  });
});
