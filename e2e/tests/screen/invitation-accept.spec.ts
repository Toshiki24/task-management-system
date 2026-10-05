import { expect, test, unique } from "../../support/fixtures";
import { signIn } from "../../support/ui";

test.describe("7.9 SCR-009 招待フロー（受諾・作成）", () => {
  test("SCR-009-01 新規ユーザーが招待を受諾してアカウント作成", async ({ page, data }) => {
    const admin = await data.createUser("WS管理者");
    const workspaceId = await data.createWorkspace(admin, "ADMIN");
    const inviteeEmail = `${unique("invitee")}@example.test`;
    const token = await data.createInvitation(workspaceId, inviteeEmail, admin, "MEMBER");

    // 未ログインのまま招待リンクを開く
    await page.goto(`/invitations/accept?token=${token}`);

    await expect(page.getByRole("heading", { name: "ワークスペースへの招待" })).toBeVisible();
    await page.getByLabel("名前 *").fill("新規 太郎");
    await page.getByLabel("パスワード *（8文字以上）").fill("Password123!");
    await page.getByRole("button", { name: "参加する" }).click();

    await expect(page.getByRole("heading", { name: "ワークスペースに参加しました" })).toBeVisible();

    // 作成したアカウントでログインできる
    const login = await page.request.post("/api/bff/auth/login", {
      headers: { Origin: new URL(page.url()).origin, "X-Requested-With": "XMLHttpRequest" },
      data: { email: inviteeEmail, password: "Password123!" },
    });
    expect(login.status()).toBe(200);
  });

  test("SCR-009-02 既存ユーザーが招待を受諾して参加", async ({ page, data }) => {
    const admin = await data.createUser("WS管理者");
    const existing = await data.createUser("既存ユーザー");
    const workspaceId = await data.createWorkspace(admin, "ADMIN");
    const token = await data.createInvitation(workspaceId, existing.email, admin, "MEMBER");

    await page.goto(`/invitations/accept?token=${token}`);

    await expect(page.getByText("既存のアカウントで参加します。")).toBeVisible();
    await page.getByRole("button", { name: "参加する" }).click();

    await expect(page.getByRole("heading", { name: "ワークスペースに参加しました" })).toBeVisible();
  });

  test("SCR-009-03 無効なトークンはエラー表示", async ({ page }) => {
    await page.goto("/invitations/accept?token=invalid-token-xyz");

    await expect(page.getByText("招待が無効か、有効期限が切れています。")).toBeVisible();
  });

  test("SCR-009-04 WS Admin が招待を作成できる", async ({ page, data }) => {
    const admin = await data.createUser("WS管理者");
    const workspaceId = await data.createWorkspace(admin, "ADMIN");
    await signIn(page, admin);

    await page.goto(`/workspaces/${workspaceId}`);
    await page.getByLabel("メールアドレス").fill(`${unique("newinvite")}@example.test`);
    await page.getByRole("button", { name: "招待する" }).click();

    await expect(page.getByText(/招待を作成しました/)).toBeVisible();
  });
});
