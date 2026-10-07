import { expect, test, unique } from "../../support/fixtures";
import { signIn } from "../../support/ui";

test.describe("7.13 SCR-013 ラベル管理(ワークスペース設定・M2)", () => {
  test("SCR-013-01 WS Admin はラベルを追加できる", async ({ page, data }) => {
    const admin = await data.createUser("WS管理者");
    const workspaceId = await data.createWorkspace(admin, "ADMIN");
    await signIn(page, admin);

    await page.goto(`/workspaces/${workspaceId}`);
    const section = page
      .locator("section")
      .filter({ has: page.getByRole("heading", { name: "ラベル", exact: true }) });

    const name = unique("label").replace(/[^a-z0-9]/gi, "").toLowerCase();
    await section.getByRole("button", { name: "ラベルを追加" }).click();
    await section.getByLabel("ラベル名").fill(name);
    await section.getByRole("button", { name: "追加", exact: true }).click();

    await expect(section.getByText(name)).toBeVisible();
  });

  test("SCR-013-02 一般メンバーはラベルを変更できない(読み取り専用)", async ({ page, data }) => {
    const admin = await data.createUser("WS管理者");
    const member = await data.createUser("一般メンバー");
    const workspaceId = await data.createWorkspace(admin, "ADMIN");
    await data.addWorkspaceMember(workspaceId, member, "MEMBER");
    await signIn(page, member);

    await page.goto(`/workspaces/${workspaceId}`);
    const section = page
      .locator("section")
      .filter({ has: page.getByRole("heading", { name: "ラベル", exact: true }) });

    await expect(section.getByRole("button", { name: "ラベルを追加" })).toHaveCount(0);
  });
});
