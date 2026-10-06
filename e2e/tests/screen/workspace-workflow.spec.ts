import { expect, test, unique } from "../../support/fixtures";
import { signIn } from "../../support/ui";

test.describe("7.11 SCR-011 ワークフロー管理(ワークスペース設定・M2)", () => {
  test("SCR-011-01 WS Admin は状態一覧を見て追加できる", async ({ page, data }) => {
    const admin = await data.createUser("WS管理者");
    const workspaceId = await data.createWorkspace(admin, "ADMIN");
    await signIn(page, admin);

    await page.goto(`/workspaces/${workspaceId}`);

    const section = page
      .locator("section")
      .filter({ has: page.getByRole("heading", { name: "ワークフロー(タスクの状態)" }) });

    // 既定の3状態が並ぶ
    await expect(section.getByRole("listitem")).toHaveCount(3);
    await expect(section.getByText("既定")).toBeVisible();

    // 状態を追加する
    await section.getByRole("button", { name: "状態を追加" }).click();
    await section.getByLabel("状態キー(英大文字)").fill(unique("REVIEW").replace(/[^A-Z0-9_]/gi, "").toUpperCase());
    await section.getByLabel("状態名").fill("レビュー中");
    await section.getByRole("button", { name: "追加", exact: true }).click();

    await expect(section.getByRole("listitem")).toHaveCount(4);
    await expect(section.getByText("レビュー中")).toBeVisible();
  });

  test("SCR-011-02 一般メンバーはワークフローを変更できない(読み取り専用)", async ({ page, data }) => {
    const admin = await data.createUser("WS管理者");
    const member = await data.createUser("一般メンバー");
    const workspaceId = await data.createWorkspace(admin, "ADMIN");
    await data.addWorkspaceMember(workspaceId, member, "MEMBER");
    await signIn(page, member);

    await page.goto(`/workspaces/${workspaceId}`);

    const section = page
      .locator("section")
      .filter({ has: page.getByRole("heading", { name: "ワークフロー(タスクの状態)" }) });

    await expect(section.getByRole("listitem")).toHaveCount(3);
    await expect(section.getByRole("button", { name: "状態を追加" })).toHaveCount(0);
  });
});
