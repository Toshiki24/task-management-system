import { randomUUID } from "node:crypto";
import { expect, test, type APIRequest, type APIRequestContext } from "@playwright/test";
import { API_URL, ORIGIN_VERIFY_SECRET } from "../../support/env";

const LOGIN_FAILED = { message: "メールアドレスまたはパスワードが正しくありません。" };
const TOO_MANY_ATTEMPTS = { message: "ログインの試行回数が上限に達しました。しばらくしてから再度お試しください。" };

/** IP単位の上限。E2Eでは playwright.config.ts(start-backend)で 8 に設定している */
const MAX_PER_IP = 8;

function notRegisteredEmail(): string {
  return `e2e-spray-${randomUUID().slice(0, 12)}@example.test`;
}

/** BFFを模して、共有シークレットと実クライアントIPを付けてAPIを直接呼ぶコンテキストを作る */
function contextForIp(request: APIRequest, ip: string): Promise<APIRequestContext> {
  return request.newContext({
    baseURL: API_URL,
    extraHTTPHeaders: { "X-Origin-Verify": ORIGIN_VERIFY_SECRET, "X-Client-IP": ip },
  });
}

test.describe("9.13 SEC2-03 ログイン試行回数のIP単位の制限（パスワードスプレー対策）", () => {
  test("SEC2-03-01 同一IPから異なるアカウントへの連続失敗は、IP単位で制限される", async ({ playwright }) => {
    const ip = `203.0.113.${Math.floor(Math.random() * 254) + 1}`;
    const context = await contextForIp(playwright.request, ip);

    // 毎回異なる(存在しない)メールで失敗させる → アカウント単位の上限には達しないが、IP単位では数えられる
    for (let i = 0; i < MAX_PER_IP; i++) {
      const res = await context.post("/api/auth/login", {
        data: { email: notRegisteredEmail(), password: "WrongPassword!" },
      });
      expect(res.status(), `${i + 1}回目はアカウント単位では制限されない`).toBe(401);
      expect(await res.json()).toEqual(LOGIN_FAILED);
    }

    const blocked = await context.post("/api/auth/login", {
      data: { email: notRegisteredEmail(), password: "WrongPassword!" },
    });

    expect(blocked.status()).toBe(429);
    expect(await blocked.json()).toEqual(TOO_MANY_ATTEMPTS);
    expect(Number(blocked.headers()["retry-after"])).toBeGreaterThan(0);

    await context.dispose();
  });

  test("SEC2-03-02 別のIPからのログインは影響を受けない", async ({ playwright }) => {
    const spraySource = await contextForIp(playwright.request, `198.51.100.${Math.floor(Math.random() * 254) + 1}`);
    for (let i = 0; i <= MAX_PER_IP; i++) {
      await spraySource.post("/api/auth/login", {
        data: { email: notRegisteredEmail(), password: "WrongPassword!" },
      });
    }

    // 別サブネットのIPは制限されない(通常の認証エラーに留まる)
    const other = await contextForIp(playwright.request, `198.51.101.${Math.floor(Math.random() * 254) + 1}`);
    const res = await other.post("/api/auth/login", {
      data: { email: notRegisteredEmail(), password: "WrongPassword!" },
    });

    expect(res.status()).toBe(401);

    await spraySource.dispose();
    await other.dispose();
  });
});
