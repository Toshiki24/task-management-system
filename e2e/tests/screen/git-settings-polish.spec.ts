import { bearer, expect, test } from "../../support/fixtures";
import { signIn } from "../../support/ui";

test.describe("7.34 SCR-034 Git 設定の仕上げ (M4)", () => {
  test("SCR-034-01 Git 接続の疎通確認で状態が有効になる", async ({ page, api, data }) => {
    const owner = await data.createUser("オーナー");
    const workspaceId = await data.createWorkspace(owner);
    await api.post(`/api/workspaces/${workspaceId}/git-connections`, {
      headers: bearer(owner),
      data: { provider: "GITHUB", authType: "GITHUB_APP", externalAccount: "acme", secretRef: "s" },
    });
    await signIn(page, owner);
    await page.goto(`/workspaces/${workspaceId}`);

    await page.getByRole("button", { name: "疎通確認" }).click();

    // 確認後、状態が「有効」で表示される
    await expect(page.getByText("有効")).toBeVisible();
  });

  test("SCR-034-02 プロジェクト個別の自動遷移ルールを保存できる", async ({ page, data }) => {
    const owner = await data.createUser("オーナー");
    const project = await data.createProject(owner);
    await signIn(page, owner);
    await page.goto(`/projects/${project.id}`);

    const section = page
      .locator("div")
      .filter({ has: page.getByRole("heading", { name: "Git 自動遷移ルール（プロジェクト個別）" }) })
      .last();
    await section.getByLabel("PR マージ時 の遷移先").selectOption({ label: "完了" });
    await section.getByRole("button", { name: "遷移ルールを更新" }).click();

    await expect(section.getByText("保存しました。")).toBeVisible();
  });
});
