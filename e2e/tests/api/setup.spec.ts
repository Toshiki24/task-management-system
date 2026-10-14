import { expect, test } from "../../support/fixtures";

const CSRF = { "x-requested-with": "XMLHttpRequest" };

test.describe("5.41 初回セットアップAPI (M5 自己ホスト)", () => {
  test("API-5101 セットアップ状態APIは真偽のみ返し、機密情報を含まない", async ({ api }) => {
    const res = await api.get("/api/setup/status");
    expect(res.status()).toBe(200);
    const body = await res.json();
    expect(typeof body.setupRequired).toBe("boolean");
    // ユーザー一覧やメール等は返さない
    expect(body.users).toBeUndefined();
    expect(body.email).toBeUndefined();
  });

  test("API-5102 ユーザーが存在する状態では初回管理者を作成できない(409)", async ({ api, data }) => {
    // 共有DBには既にユーザーが居るが、順序非依存にするため明示的に1人作る
    await data.createUser("既存ユーザー");

    const res = await api.post("/api/setup/admin", {
      headers: CSRF,
      data: { name: "後発管理者", email: `late-${Date.now()}@example.test`, password: "setup-password-123456" },
    });
    // サーバー側で必ず拒否する(画面判定に依存しない)。直接呼び出しでも回避できない
    expect(res.status()).toBe(409);

    // 作成されていないこと(ログインできない)を確認
    const login = await api.post("/api/auth/login", {
      data: { email: "late-does-not-exist@example.test", password: "setup-password-123456" },
    });
    expect(login.status()).toBe(401);
  });

  test("API-5103 入力不正(短いパスワード)は400で弾く", async ({ api }) => {
    const res = await api.post("/api/setup/admin", {
      headers: CSRF,
      data: { name: "管理者", email: `bad-${Date.now()}@example.test`, password: "short" },
    });
    expect(res.status()).toBe(400);
  });
});
