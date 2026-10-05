import { expect, test } from "../../support/fixtures";
import { signIn } from "../../support/ui";

test.describe("7.8 SCR-008 ワークスペース管理画面", () => {
  test("SCR-008-01 WS Admin はメンバー一覧を見て追加できる", async ({ page, data }) => {
    const admin = await data.createUser("WS管理者");
    const target = await data.createUser("追加対象");
    const workspaceId = await data.createWorkspace(admin, "ADMIN");
    await signIn(page, admin);

    await page.goto(`/workspaces/${workspaceId}`);

    // 自分(ADMIN)が一覧に出る
    await expect(page.getByRole("listitem").filter({ hasText: admin.name })).toBeVisible();

    // 既存ユーザーを MEMBER として追加(メンバー追加フォームに絞って操作する)
    await page.getByRole("button", { name: "メンバー追加" }).click();
    const addForm = page.locator("form").filter({ has: page.getByLabel("ユーザー") });
    await addForm.getByLabel("ユーザー").selectOption({ label: `${target.name} (${target.email})` });
    await addForm.getByLabel("ロール").selectOption("MEMBER");
    await addForm.getByRole("button", { name: "追加", exact: true }).click();

    await expect(page.getByRole("listitem").filter({ hasText: target.name })).toBeVisible();
  });

  test("SCR-008-02 WS Admin はメンバーのロールを変更できる", async ({ page, data }) => {
    const admin = await data.createUser("WS管理者");
    const member = await data.createUser("一般メンバー");
    const workspaceId = await data.createWorkspace(admin, "ADMIN");
    await data.addWorkspaceMember(workspaceId, member, "MEMBER");
    await signIn(page, admin);

    await page.goto(`/workspaces/${workspaceId}`);

    const roleSelect = page.getByLabel(`${member.name} のロール`);
    await expect(roleSelect).toHaveValue("MEMBER");
    await roleSelect.selectOption("VIEWER");

    // 反映後も VIEWER のまま(再取得後の値)
    await expect(page.getByLabel(`${member.name} のロール`)).toHaveValue("VIEWER");
  });

  test("SCR-008-03 WS Admin はメンバーを削除できる", async ({ page, data }) => {
    const admin = await data.createUser("WS管理者");
    const member = await data.createUser("削除対象");
    const workspaceId = await data.createWorkspace(admin, "ADMIN");
    await data.addWorkspaceMember(workspaceId, member, "MEMBER");
    await signIn(page, admin);

    await page.goto(`/workspaces/${workspaceId}`);
    await expect(page.getByRole("listitem").filter({ hasText: member.name })).toBeVisible();

    await page
      .getByRole("listitem")
      .filter({ hasText: member.name })
      .getByRole("button", { name: "削除" })
      .click();
    await page
      .locator("div.fixed.inset-0")
      .filter({ has: page.getByRole("heading", { name: "メンバー削除" }) })
      .getByRole("button", { name: "削除" })
      .click();

    await expect(page.getByRole("listitem").filter({ hasText: member.name })).toHaveCount(0);
  });

  test("SCR-008-04 一般メンバーは管理操作ができない(読み取り専用)", async ({ page, data }) => {
    const admin = await data.createUser("WS管理者");
    const member = await data.createUser("閲覧メンバー");
    const workspaceId = await data.createWorkspace(admin, "ADMIN");
    await data.addWorkspaceMember(workspaceId, member, "MEMBER");
    await signIn(page, member);

    await page.goto(`/workspaces/${workspaceId}`);

    await expect(page.getByText("メンバーの管理はワークスペース管理者(ADMIN)のみ可能です。")).toBeVisible();
    await expect(page.getByRole("button", { name: "メンバー追加" })).toHaveCount(0);
    // ロールはテキスト表示(変更用セレクトは出さない)
    await expect(page.getByLabel(`${member.name} のロール`)).toHaveCount(0);
  });

  test("SCR-008-05 プロジェクト一覧からワークスペース設定へ遷移できる", async ({ page, data }) => {
    const admin = await data.createUser("WS管理者");
    const workspaceId = await data.createWorkspace(admin, "ADMIN");
    await signIn(page, admin);

    await page.goto("/projects");
    await page.getByRole("link", { name: "ワークスペース設定" }).click();

    await expect(page).toHaveURL(`/workspaces/${workspaceId}`);
    await expect(page.getByRole("heading", { name: "メンバー", exact: true })).toBeVisible();
  });
});
