import { expect, test } from "../../support/fixtures";
import { signIn } from "../../support/ui";

test.describe("7.30 SCR-030 Git ユーザー対応付け (M4)", () => {
  test("SCR-030-01 ワークスペース設定で Git ユーザーを対応付けできる", async ({ page, data }) => {
    const owner = await data.createUser("オーナー");
    const workspaceId = await data.createWorkspace(owner);
    await signIn(page, owner);
    await page.goto(`/workspaces/${workspaceId}`);

    await page.getByRole("button", { name: "対応付けを追加" }).click();
    await page.getByLabel("メンバー").selectOption({ index: 1 });
    await page.getByLabel("Git ユーザーID(プロバイダ側の安定ID)").fill("123456");
    await page.getByLabel("Git ユーザー名(任意・表示用)").fill("octocat");
    await page.getByRole("button", { name: "追加", exact: true }).click();

    await expect(page.getByText("octocat")).toBeVisible();
  });
});
