import { bearer, expect, test } from "../../support/fixtures";
import { signIn } from "../../support/ui";
import { totp } from "../../support/totp";

test.describe("7.39 SCR-039 MFA(TOTP) (M5)", () => {
  test("SCR-039-01 セキュリティ設定から MFA を有効化できる", async ({ page, data }) => {
    const user = await data.createUser("MFA設定");
    await signIn(page, user);
    await page.goto("/me/security");

    await expect(page.getByText("無効", { exact: true })).toBeVisible();
    await page.getByRole("button", { name: "MFA を有効にする" }).click();

    // 画面に表示されたシークレットから現在のコードを計算して入力する
    const secret = (await page.getByTestId("mfa-secret").textContent())?.trim() ?? "";
    expect(secret.length).toBeGreaterThan(0);
    await page.getByLabel("認証アプリに表示されたコード").fill(totp(secret));
    await page.getByRole("button", { name: "確認して有効化" }).click();

    // リカバリコードが表示され、状態が「有効」になる
    await expect(page.getByTestId("recovery-codes")).toBeVisible();
    await expect(page.getByTestId("recovery-codes").getByRole("listitem")).toHaveCount(10);
    await expect(page.getByText("有効", { exact: true })).toBeVisible();
  });

  test("SCR-039-02 MFA 有効ユーザーはログインでコード入力を求められる", async ({ page, data, api }) => {
    const user = await data.createUser("MFAログイン");
    // 事前に API で MFA を有効化しておく
    const enroll = await (await api.post("/api/mfa/enroll", { headers: bearer(user) })).json();
    await api.post("/api/mfa/confirm", { headers: bearer(user), data: { code: totp(enroll.secret) } });

    await page.goto("/login");
    await page.getByLabel("メールアドレス").fill(user.email);
    await page.getByLabel("パスワード").fill(user.password);
    await page.getByRole("button", { name: "ログイン" }).click();

    // コード入力ステップに進む
    const codeField = page.getByLabel("認証コード");
    await expect(codeField).toBeVisible();
    await codeField.fill(totp(enroll.secret));
    await page.getByRole("button", { name: "認証して続行" }).click();

    await expect(page).toHaveURL("/projects");
    await expect(page.getByRole("banner")).toContainText(user.name);
  });
});
