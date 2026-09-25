import { bearer, expect, NON_EXISTENT_ID, test } from "../../support/fixtures";

test.describe("5.2 ユーザーAPI", () => {
  test("API-901 ユーザー一覧取得", async ({ api, data }) => {
    const user = await data.createUser();
    const other = await data.createUser();

    const response = await api.get("/api/users", { headers: bearer(user) });

    expect(response.status()).toBe(200);
    const users: Record<string, unknown>[] = await response.json();
    // 他のテストが作成したユーザーも含まれるため、件数ではなく包含で検証する
    expect(users).toEqual(
      expect.arrayContaining([
        { id: user.id, name: user.name, email: user.email },
        { id: other.id, name: other.name, email: other.email },
      ]),
    );
    for (const item of users) {
      expect(Object.keys(item).sort()).toEqual(["email", "id", "name"]);
    }
  });

  test("API-902 ユーザー詳細取得（存在する）", async ({ api, data }) => {
    const user = await data.createUser();
    const target = await data.createUser();

    const response = await api.get(`/api/users/${target.id}`, { headers: bearer(user) });

    expect(response.status()).toBe(200);
    expect(await response.json()).toEqual({ id: target.id, name: target.name, email: target.email });
  });

  test("API-903 ユーザー詳細取得（存在しない）", async ({ api, data }) => {
    const user = await data.createUser();

    const response = await api.get(`/api/users/${NON_EXISTENT_ID}`, { headers: bearer(user) });

    expect(response.status()).toBe(404);
    expect(await response.json()).toEqual({ message: "指定されたユーザーが存在しません。" });
  });
});
