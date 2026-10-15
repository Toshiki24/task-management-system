import { bearer, expect, test } from "../../support/fixtures";

test.describe("5.39 通知設定API (M5)", () => {
  test("API-4901 メール通知設定は既定で有効、更新すると反映される", async ({ data, api }) => {
    const user = await data.createUser("通知設定");

    // 既定は有効
    const initial = await api.get("/api/users/me/notification-settings", { headers: bearer(user) });
    expect(initial.status()).toBe(200);
    expect((await initial.json()).emailEnabled).toBe(true);

    // 無効化
    const off = await api.put("/api/users/me/notification-settings", {
      headers: bearer(user),
      data: { emailEnabled: false },
    });
    expect(off.status()).toBe(200);
    expect((await off.json()).emailEnabled).toBe(false);

    // 取得し直しても無効のまま
    const reread = await api.get("/api/users/me/notification-settings", { headers: bearer(user) });
    expect((await reread.json()).emailEnabled).toBe(false);

    // 再度有効化
    const on = await api.put("/api/users/me/notification-settings", {
      headers: bearer(user),
      data: { emailEnabled: true },
    });
    expect((await on.json()).emailEnabled).toBe(true);
  });

  test("API-4902 未認証では通知設定にアクセスできない(401)", async ({ api }) => {
    const res = await api.get("/api/users/me/notification-settings");
    expect(res.status()).toBe(401);
  });
});
