import { expect, expectValidationError, PASSWORD, test } from "../../support/fixtures";

const LOGIN_FAILED = { message: "メールアドレスまたはパスワードが正しくありません。" };

test.describe("5.1 認証API", () => {
  test("API-801 ログイン成功", async ({ api, data }) => {
    const user = await data.createUser();

    const response = await api.post("/api/auth/login", { data: { email: user.email, password: PASSWORD } });

    expect(response.status()).toBe(200);
    const body = await response.json();
    expect(body.accessToken).toEqual(expect.any(String));
    expect(body.accessToken.length).toBeGreaterThan(0);
    expect(body.user).toEqual({ id: user.id, name: user.name, email: user.email });
  });

  test("API-802 メール未指定", async ({ api }) => {
    const response = await api.post("/api/auth/login", { data: { password: PASSWORD } });

    await expectValidationError(response, "email", "メールアドレスは必須です。");
  });

  test("API-803 パスワード未指定", async ({ api, data }) => {
    const user = await data.createUser();

    const response = await api.post("/api/auth/login", { data: { email: user.email } });

    await expectValidationError(response, "password", "パスワードは必須です。");
  });

  test("API-804 メール形式不正", async ({ api }) => {
    const response = await api.post("/api/auth/login", { data: { email: "not-an-email", password: PASSWORD } });

    await expectValidationError(response, "email", "メールアドレスの形式が正しくありません。");
  });

  test("API-805 存在しないメールでログイン", async ({ api }) => {
    const response = await api.post("/api/auth/login", {
      data: { email: "e2e-not-registered@example.test", password: PASSWORD },
    });

    expect(response.status()).toBe(401);
    expect(await response.json()).toEqual(LOGIN_FAILED);
  });

  test("API-806 パスワード誤り", async ({ api, data }) => {
    const user = await data.createUser();

    const response = await api.post("/api/auth/login", { data: { email: user.email, password: "WrongPassword!" } });

    expect(response.status()).toBe(401);
    expect(await response.json()).toEqual(LOGIN_FAILED);
  });
});
