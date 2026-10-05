import { createHash } from "node:crypto";
import type { APIRequestContext } from "@playwright/test";
import { bearer, expect, PASSWORD, test, type TestUser } from "../../support/fixtures";

const REFRESH_FAILED = { message: "認証の有効期限が切れました。再度ログインしてください。" };

/** 置き換え済みトークンの再利用を許容する猶予期間(E2E用に scripts/start-backend.mjs で2秒に設定) */
const REUSE_GRACE_MS = 2_000;

interface TokenResponse {
  accessToken: string;
  accessTokenExpiresAt: string;
  refreshToken: string;
  user: { id: number; name: string; email: string };
}

async function login(api: APIRequestContext, user: TestUser): Promise<TokenResponse> {
  const response = await api.post("/api/auth/login", { data: { email: user.email, password: PASSWORD } });
  expect(response.status()).toBe(200);
  return response.json();
}

function refresh(api: APIRequestContext, refreshToken: string) {
  return api.post("/api/auth/refresh", { data: { refreshToken } });
}

test.describe("9.7 SEC-05 リフレッシュトークン（API）", () => {
  test("SEC-05-01 ログインでリフレッシュトークンが発行され、アクセストークンは短命である", async ({ api, data }) => {
    const user = await data.createUser();

    const body = await login(api, user);

    expect(body.refreshToken).toEqual(expect.any(String));
    expect(body.refreshToken.length).toBeGreaterThanOrEqual(43);
    const remainingMs = new Date(body.accessTokenExpiresAt).getTime() - Date.now();
    expect(remainingMs).toBeGreaterThan(0);
    expect(remainingMs).toBeLessThanOrEqual(15 * 60 * 1000 + 5_000);
  });

  test("SEC-05-02 リフレッシュでアクセストークンとリフレッシュトークンが再発行される", async ({ api, data }) => {
    const user = await data.createUser();
    const logged = await login(api, user);

    const response = await refresh(api, logged.refreshToken);

    expect(response.status()).toBe(200);
    const body: TokenResponse = await response.json();
    expect(body.refreshToken).not.toBe(logged.refreshToken);
    expect(body.user).toEqual({ id: user.id, name: user.name, email: user.email });
    // 再発行したアクセストークンでAPIを呼び出せる
    const projects = await api.get("/api/me/workspaces", { headers: { Authorization: `Bearer ${body.accessToken}` } });
    expect(projects.status()).toBe(200);
  });

  test("SEC-05-03 猶予期間を過ぎて置き換え済みのトークンが使われると、全トークンが失効する", async ({ api, data }) => {
    const user = await data.createUser();
    const logged = await login(api, user);
    const rotated: TokenResponse = await (await refresh(api, logged.refreshToken)).json();

    await new Promise((resolve) => setTimeout(resolve, REUSE_GRACE_MS + 500));
    const reused = await refresh(api, logged.refreshToken);

    expect(reused.status()).toBe(401);
    expect(await reused.json()).toEqual(REFRESH_FAILED);
    // 漏洩とみなして、正規の利用者が持つ最新のトークンも失効させる
    const latest = await refresh(api, rotated.refreshToken);
    expect(latest.status()).toBe(401);
  });

  test("SEC-05-04 猶予期間内なら、置き換え済みのトークンでも再発行できる（同時リクエスト対策）", async ({ api, data }) => {
    const user = await data.createUser();
    const logged = await login(api, user);

    // 同じトークンで同時に2回リフレッシュする
    const [first, second] = await Promise.all([
      refresh(api, logged.refreshToken),
      refresh(api, logged.refreshToken),
    ]);

    expect(first.status()).toBe(200);
    expect(second.status()).toBe(200);
  });

  test("SEC-05-05 ログアウトしたリフレッシュトークンでは再発行できない", async ({ api, data }) => {
    const user = await data.createUser();
    const logged = await login(api, user);

    const logout = await api.post("/api/auth/logout", { data: { refreshToken: logged.refreshToken } });
    expect(logout.status()).toBe(204);

    const response = await refresh(api, logged.refreshToken);
    expect(response.status()).toBe(401);
    expect(await response.json()).toEqual(REFRESH_FAILED);
  });

  test("SEC-05-06 DBにはリフレッシュトークンのハッシュ値のみが保存される", async ({ api, data, db }) => {
    const user = await data.createUser();
    const logged = await login(api, user);

    const hash = createHash("sha256").update(logged.refreshToken).digest("hex");
    const { rows } = await db.query<{ token_hash: string }>(
      "SELECT token_hash FROM refresh_tokens WHERE user_id = $1",
      [user.id],
    );
    const hashes = rows.map((r) => r.token_hash);
    expect(hashes).toContain(hash);
    expect(hashes).not.toContain(logged.refreshToken);
  });

  test("SEC-05-07 リフレッシュトークンではAPIを呼び出せない", async ({ api, data }) => {
    const user = await data.createUser();
    const logged = await login(api, user);

    const response = await api.get("/api/me/workspaces", { headers: bearer({ ...user, token: logged.refreshToken }) });

    expect(response.status()).toBe(401);
  });
});
