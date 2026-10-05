import { expect, test, unique } from "../../support/fixtures";
import { signIn } from "../../support/ui";

test.describe("7.10 SCR-010 ワークスペース作成（System Admin）", () => {
  test("SCR-010-01 System Admin はワークスペースを作成し、作成者が管理できる", async ({ page, data }) => {
    const admin = await data.createUser("システム管理者");
    await data.makeSystemAdmin(admin); // ログイン前に付与(セッションに反映させる)
    await signIn(page, admin);

    await page.goto("/workspaces/new");
    const name = unique("E2E新規WS");
    await page.getByLabel("ワークスペース名 *").fill(name);
    await page.getByRole("button", { name: "作成" }).click();

    // 作成後はそのワークスペースの設定画面へ遷移し、作成者は ADMIN として管理できる
    await expect(page).toHaveURL(/\/workspaces\/\d+$/);
    await expect(page.getByRole("heading", { name: name })).toBeVisible();
    await expect(page.getByRole("button", { name: "メンバー追加" })).toBeVisible();
    await expect(page.getByRole("listitem").filter({ hasText: admin.name })).toBeVisible();
  });

  test("SCR-010-02 一般ユーザーは作成画面で権限エラー", async ({ page, data }) => {
    const user = await data.createUser("一般ユーザー");
    await signIn(page, user);

    await page.goto("/workspaces/new");

    await expect(page.getByText("ワークスペースの作成は System Admin のみ可能です。")).toBeVisible();
  });

  test("SCR-010-03 System Admin は一覧画面から作成導線が見える", async ({ page, data }) => {
    const admin = await data.createUser("システム管理者");
    await data.makeSystemAdmin(admin);
    await signIn(page, admin);

    // 所属ワークスペースが無い System Admin でも作成導線が出る
    await page.goto("/projects");

    await expect(page.getByRole("link", { name: "＋ ワークスペースを作成" })).toBeVisible();
  });

  test("SCR-010-04 一般ユーザーには作成導線が出ない", async ({ page, data }) => {
    const user = await data.createUser("一般ユーザー");
    const workspaceId = await data.createWorkspace(user, "MEMBER");
    await signIn(page, user);

    await page.goto("/projects");

    // ワークスペースはあるが、作成導線(＋ワークスペース)は System Admin のみ
    await expect(page.getByRole("link", { name: "ワークスペース設定" })).toBeVisible();
    await expect(page.getByRole("link", { name: "＋ ワークスペース", exact: true })).toHaveCount(0);
    expect(workspaceId).toBeGreaterThan(0);
  });
});
