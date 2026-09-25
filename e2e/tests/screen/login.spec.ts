import { expect, test } from "../../support/fixtures";
import { alertMessage, expectNoRequestSent, recordApiRequests, validity } from "../../support/ui";

test.describe("7.1 SCR-001 ログイン画面", () => {
  test("SCR-001-01 ログイン成功", async ({ page, data }) => {
    const user = await data.createUser();

    await page.goto("/login");
    await page.getByLabel("メールアドレス").fill(user.email);
    await page.getByLabel("パスワード").fill(user.password);
    await page.getByRole("button", { name: "ログイン" }).click();

    await expect(page).toHaveURL("/projects");
    const session = await page.evaluate(() => ({
      token: localStorage.getItem("accessToken"),
      user: JSON.parse(localStorage.getItem("currentUser") ?? "null"),
    }));
    expect(session.token).toBeTruthy();
    expect(session.user).toEqual({ id: user.id, name: user.name, email: user.email });
  });

  test("SCR-001-02 ログイン失敗", async ({ page, data }) => {
    const user = await data.createUser();

    await page.goto("/login");
    await page.getByLabel("メールアドレス").fill(user.email);
    await page.getByLabel("パスワード").fill("WrongPassword!");
    await page.getByRole("button", { name: "ログイン" }).click();

    await expect(alertMessage(page)).toHaveText("メールアドレスまたはパスワードが正しくありません。");
    await expect(page).toHaveURL("/login");
  });

  test("SCR-001-03 メールアドレス未入力", async ({ page }) => {
    const loginRequests = recordApiRequests(page, "POST", /^\/api\/auth\/login$/);

    await page.goto("/login");
    await page.getByLabel("パスワード").fill("Password123!");
    await page.getByRole("button", { name: "ログイン" }).click();

    expect(await validity(page.getByLabel("メールアドレス"))).toMatchObject({ valueMissing: true });
    await expect(page).toHaveURL("/login");
    await expectNoRequestSent(page, loginRequests);
  });

  test("SCR-001-04 メールアドレス形式不正", async ({ page }) => {
    const loginRequests = recordApiRequests(page, "POST", /^\/api\/auth\/login$/);

    await page.goto("/login");
    await page.getByLabel("メールアドレス").fill("abc");
    await page.getByLabel("パスワード").fill("Password123!");
    await page.getByRole("button", { name: "ログイン" }).click();

    expect(await validity(page.getByLabel("メールアドレス"))).toMatchObject({ typeMismatch: true });
    await expect(page).toHaveURL("/login");
    await expectNoRequestSent(page, loginRequests);
  });

  test("SCR-001-05 未ログインで保護画面にアクセス", async ({ page }) => {
    await page.goto("/login");
    await page.evaluate(() => localStorage.removeItem("accessToken"));

    await page.goto("/projects");

    await expect(page).toHaveURL("/login");
  });
});
