import { expect, test } from "../../support/fixtures";

test.describe("5.40 ヘルスチェック (M5 自己ホスト)", () => {
  test("API-5001 /health は認証なしで 200 と status:ok を返す", async ({ api }) => {
    const res = await api.get("/health");
    expect(res.status()).toBe(200);
    const body = await res.json();
    expect(body.status).toBe("ok");
  });
});
