import { bearer, expect, test } from "../../support/fixtures";
import { totp } from "../../support/totp";

/** enroll→confirm で MFA を有効化し、確認に使ったシークレットを返す。 */
async function enableMfa(api: any, user: any): Promise<string> {
  const enroll = await (await api.post("/api/mfa/enroll", { headers: bearer(user) })).json();
  const confirm = await api.post("/api/mfa/confirm", {
    headers: bearer(user),
    data: { code: totp(enroll.secret) },
  });
  expect(confirm.status()).toBe(200);
  return enroll.secret;
}

test.describe("5.39 MFA(TOTP)API (M5)", () => {
  test("API-4901 有効化後のログインはMFAを要求し、TOTPで検証するとセッションを発行する", async ({ data, api }) => {
    const user = await data.createUser("MFA利用者");
    const secret = await enableMfa(api, user);

    // 一次認証(パスワード)だけではトークンを発行せず、MFA を要求する
    const login = await api.post("/api/auth/login", {
      data: { email: user.email, password: user.password },
    });
    expect(login.status()).toBe(200);
    const loginBody = await login.json();
    expect(loginBody.mfaRequired).toBe(true);
    expect(typeof loginBody.mfaToken).toBe("string");
    expect(loginBody.accessToken).toBeUndefined();

    // MFA 検証でセッション(トークン)を発行する
    const verify = await api.post("/api/auth/mfa/verify", {
      data: { mfaToken: loginBody.mfaToken, code: totp(secret) },
    });
    expect(verify.status()).toBe(200);
    const verifyBody = await verify.json();
    expect(typeof verifyBody.accessToken).toBe("string");
    expect(verifyBody.user.id).toBe(user.id);
  });

  test("API-4902 リカバリコードでログイン検証でき、同じコードは再利用できない", async ({ data, api }) => {
    const user = await data.createUser("MFA利用者");
    const enroll = await (await api.post("/api/mfa/enroll", { headers: bearer(user) })).json();
    const confirm = await (await api.post("/api/mfa/confirm", {
      headers: bearer(user),
      data: { code: totp(enroll.secret) },
    })).json();
    const recovery: string = confirm.recoveryCodes[0];

    // 1回目: リカバリコードで検証成功
    const login1 = await (await api.post("/api/auth/login", {
      data: { email: user.email, password: user.password },
    })).json();
    const verify1 = await api.post("/api/auth/mfa/verify", {
      data: { mfaToken: login1.mfaToken, code: recovery },
    });
    expect(verify1.status()).toBe(200);

    // 2回目: 同じリカバリコードは使えない(401)
    const login2 = await (await api.post("/api/auth/login", {
      data: { email: user.email, password: user.password },
    })).json();
    const verify2 = await api.post("/api/auth/mfa/verify", {
      data: { mfaToken: login2.mfaToken, code: recovery },
    });
    expect(verify2.status()).toBe(401);
  });

  test("API-4903 誤コードでの確認は400、MFA未設定の無効化は409", async ({ data, api }) => {
    const user = await data.createUser("MFA利用者");
    await api.post("/api/mfa/enroll", { headers: bearer(user) });

    const badConfirm = await api.post("/api/mfa/confirm", {
      headers: bearer(user),
      data: { code: "000000" },
    });
    expect(badConfirm.status()).toBe(400);

    // まだ有効化していないので無効化は 409
    const disable = await api.post("/api/mfa/disable", {
      headers: bearer(user),
      data: { code: "000000" },
    });
    expect(disable.status()).toBe(409);
  });

  test("API-4904 MFA設定APIは未認証では401", async ({ api }) => {
    const res = await api.get("/api/mfa");
    expect(res.status()).toBe(401);
  });
});
