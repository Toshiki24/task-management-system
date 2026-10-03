import { API_URL } from "../../support/env";
import { bearer, expect, test } from "../../support/fixtures";

const ACCESS_DENIED = { message: "アクセスが拒否されました。" };

/**
 * 公開されている API Gateway のURLを、BFFを経由せずに直接呼び出す攻撃を防ぐ
 * 共有シークレット(X-Origin-Verify)の検証を確認する(security-review-2.md SEC2-01、aws-architecture.md 5.3)。
 */
test.describe("9.12 SEC2-01 APIへの直接アクセスの制限（X-Origin-Verify）", () => {
  test("SEC2-01-01 共有シークレットなしの直接呼び出しは403（認証より前に拒否）", async ({ playwright, data }) => {
    const user = await data.createUser();
    // 正しい認証情報でも、共有シークレットがなければ認証処理に到達せず403になる
    const context = await playwright.request.newContext({ baseURL: API_URL });

    const login = await context.post("/api/auth/login", {
      data: { email: user.email, password: user.password },
    });
    const users = await context.get("/api/users", { headers: bearer(user) });

    expect(login.status()).toBe(403);
    expect(await login.json()).toEqual(ACCESS_DENIED);
    expect(users.status()).toBe(403);
    await context.dispose();
  });

  test("SEC2-01-02 誤った共有シークレットの直接呼び出しは403", async ({ playwright }) => {
    const context = await playwright.request.newContext({
      baseURL: API_URL,
      extraHTTPHeaders: { "X-Origin-Verify": "wrong-secret" },
    });

    const response = await context.get("/api/users");

    expect(response.status()).toBe(403);
    expect(await response.json()).toEqual(ACCESS_DENIED);
    await context.dispose();
  });

  test("SEC2-01-03 正しい共有シークレットがあれば検証を通過する（未認証なら401）", async ({ api }) => {
    // api フィクスチャは BFF と同じ共有シークレットを付けるため、403ではなく通常の認証(401)に進む
    const response = await api.get("/api/users");

    expect(response.status()).toBe(401);
  });
});
