import bcrypt from "bcryptjs";
import { randomUUID } from "node:crypto";
import type { APIRequestContext } from "@playwright/test";
import { expect, PASSWORD, test } from "../../support/fixtures";

const LOGIN_FAILED = { message: "メールアドレスまたはパスワードが正しくありません。" };
const TOO_MANY_ATTEMPTS = { message: "ログインの試行回数が上限に達しました。しばらくしてから再度お試しください。" };

/** ログイン失敗の上限回数(appsettings.json の LoginProtection:MaxFailedAttempts) */
const MAX_FAILED_ATTEMPTS = 5;
/** 失敗回数を数える期間(appsettings.json の LoginProtection:FailureWindowSeconds) */
const FAILURE_WINDOW_SECONDS = 60;

function login(api: APIRequestContext, email: string, password: string) {
  return api.post("/api/auth/login", { data: { email, password } });
}

async function failLogin(api: APIRequestContext, email: string, times: number): Promise<void> {
  for (let i = 0; i < times; i++) {
    const response = await login(api, email, "WrongPassword!");
    expect(response.status(), `${i + 1}回目の失敗は通常の認証エラーになること`).toBe(401);
    expect(await response.json()).toEqual(LOGIN_FAILED);
  }
}

function notRegisteredEmail(): string {
  return `e2e-not-registered-${randomUUID().slice(0, 12)}@example.test`;
}

function median(values: number[]): number {
  const sorted = [...values].sort((a, b) => a - b);
  return sorted[Math.floor(sorted.length / 2)];
}

test.describe("9.5 SEC-02 ログイン試行回数の制限", () => {
  test("SEC-02-01 同一アカウントで上限回数失敗すると、正しいパスワードでもログインできなくなる", async ({ api, data }) => {
    const user = await data.createUser();
    await failLogin(api, user.email, MAX_FAILED_ATTEMPTS);

    const response = await login(api, user.email, PASSWORD);

    expect(response.status()).toBe(429);
    expect(await response.json()).toEqual(TOO_MANY_ATTEMPTS);
    const retryAfter = Number(response.headers()["retry-after"]);
    expect(retryAfter).toBeGreaterThan(0);
    expect(retryAfter).toBeLessThanOrEqual(FAILURE_WINDOW_SECONDS);
  });

  test("SEC-02-02 制限中のアカウントがあっても、他のアカウントはログインできる", async ({ api, data }) => {
    const locked = await data.createUser("制限対象");
    const other = await data.createUser("別ユーザー");
    await failLogin(api, locked.email, MAX_FAILED_ATTEMPTS);
    expect((await login(api, locked.email, PASSWORD)).status()).toBe(429);

    const response = await login(api, other.email, PASSWORD);

    expect(response.status()).toBe(200);
  });

  test("SEC-02-03 存在しないメールアドレスでも同じ回数で制限される", async ({ api }) => {
    // 登録済みかどうかで制限の挙動が変わると、429の有無からメールアドレスの登録有無を推測できてしまう
    const email = notRegisteredEmail();
    await failLogin(api, email, MAX_FAILED_ATTEMPTS);

    const response = await login(api, email, PASSWORD);

    expect(response.status()).toBe(429);
    expect(await response.json()).toEqual(TOO_MANY_ATTEMPTS);
  });

  test("SEC-02-04 上限未満の失敗ならログインでき、成功すると失敗回数がリセットされる", async ({ api, data }) => {
    const user = await data.createUser();

    await failLogin(api, user.email, MAX_FAILED_ATTEMPTS - 1);
    expect((await login(api, user.email, PASSWORD)).status()).toBe(200);

    // リセットされていなければ、ここで失敗回数の合計が上限を超えて429になる
    await failLogin(api, user.email, MAX_FAILED_ATTEMPTS - 1);
    expect((await login(api, user.email, PASSWORD)).status()).toBe(200);
  });

  test("SEC-02-05 メールアドレスの大文字・小文字を変えても同じアカウントとして数えられる", async ({ api, data }) => {
    const user = await data.createUser();
    await failLogin(api, user.email, 3);
    await failLogin(api, user.email.toUpperCase(), MAX_FAILED_ATTEMPTS - 3);

    const response = await login(api, user.email, PASSWORD);

    expect(response.status()).toBe(429);
  });
});

test.describe("9.6 SEC-03 ユーザー存在の推測防止", () => {
  test("SEC-03-01 存在しないメールアドレスでも、パスワード誤りと同程度の応答時間になる", async ({ api, data, db }) => {
    // 本番のユーザーと同じコスト(BCrypt.Net の既定値11)でハッシュ化したユーザーを用意する
    // (テストデータのユーザーは実行時間短縮のためコスト4でハッシュ化しており、比較に使えない)
    const user = await data.createUser();
    await db.query("UPDATE users SET password_hash = $1 WHERE id = $2", [await bcrypt.hash(PASSWORD, 11), user.id]);

    // 上限回数に達しないよう、どちらも上限未満の回数だけ計測する
    const attempts = MAX_FAILED_ATTEMPTS - 1;
    const wrongPasswordTimes: number[] = [];
    const notRegisteredTimes: number[] = [];
    for (let i = 0; i < attempts; i++) {
      let start = performance.now();
      expect((await login(api, user.email, "WrongPassword!")).status()).toBe(401);
      wrongPasswordTimes.push(performance.now() - start);

      start = performance.now();
      expect((await login(api, notRegisteredEmail(), "WrongPassword!")).status()).toBe(401);
      notRegisteredTimes.push(performance.now() - start);
    }

    // パスワード照合(BCrypt)を省略していると、存在しないメールアドレスの応答だけが桁違いに速くなる
    const ratio = median(notRegisteredTimes) / median(wrongPasswordTimes);
    expect(
      ratio,
      `応答時間の中央値 存在しないメール: ${median(notRegisteredTimes).toFixed(1)}ms / ` +
        `パスワード誤り: ${median(wrongPasswordTimes).toFixed(1)}ms`,
    ).toBeGreaterThan(0.5);
  });
});
