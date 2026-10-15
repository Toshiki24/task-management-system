import { expect, test } from "../../support/fixtures";

test.describe("5.40 ヘルスチェック・可観測性 (M5 自己ホスト/運用)", () => {
  test("API-5001 /health は認証なしで 200 と status:ok、DB チェックを返す", async ({ api }) => {
    const res = await api.get("/health");
    expect(res.status()).toBe(200);
    const body = await res.json();
    expect(body.status).toBe("ok");
    // DB 接続・マイグレーション状態のチェックが含まれる(M5 §9)
    expect(body.checks.database.status).toBe("ok");
  });

  test("API-5002 相関 ID が無ければ生成し、あれば引き継いで返す(X-Request-Id)", async ({ api }) => {
    // 無指定 → サーバーが生成して返す
    const generated = await api.get("/health");
    expect(generated.headers()["x-request-id"]).toBeTruthy();

    // 指定あり → そのまま引き継がれる
    const correlationId = "e2e-correlation-12345";
    const passed = await api.get("/health", { headers: { "X-Request-Id": correlationId } });
    expect(passed.headers()["x-request-id"]).toBe(correlationId);
  });
});
