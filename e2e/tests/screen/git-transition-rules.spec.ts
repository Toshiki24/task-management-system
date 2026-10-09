import { expect, test } from "../../support/fixtures";
import { signIn } from "../../support/ui";

test.describe("7.32 SCR-032 Git 自動遷移ルール (M4)", () => {
  test("SCR-032-01 ワークスペース設定で自動遷移ルールを保存できる", async ({ page, data }) => {
    const owner = await data.createUser("オーナー");
    const workspaceId = await data.createWorkspace(owner);
    await signIn(page, owner);
    await page.goto(`/workspaces/${workspaceId}`);

    // 「PR マージ時」の遷移先に「完了」を選ぶ
    await page.getByLabel("PR マージ時 の遷移先").selectOption({ label: "完了" });
    await page.getByRole("button", { name: "遷移ルールを更新" }).click();

    await expect(page.getByText("保存しました。")).toBeVisible();
  });
});
