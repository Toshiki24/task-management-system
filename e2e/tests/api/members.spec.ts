import { bearer, expect, expectValidationError, NON_EXISTENT_ID, test } from "../../support/fixtures";

const PROJECT_NOT_FOUND = { message: "指定されたプロジェクトが存在しません。" };

test.describe("5.4 プロジェクトメンバーAPI", () => {
  test("API-1501 メンバー一覧取得", async ({ api, data }) => {
    const owner = await data.createUser("オーナー");
    const member = await data.createUser("メンバー");
    const project = await data.createProject(owner);
    await data.addMember(project.id, owner, member);

    const response = await api.get(`/api/projects/${project.id}/members`, { headers: bearer(owner) });

    expect(response.status()).toBe(200);
    expect(await response.json()).toEqual([
      { userId: owner.id, name: owner.name, email: owner.email, role: "OWNER" },
      { userId: member.id, name: member.name, email: member.email, role: "MEMBER" },
    ]);
  });

  test("API-1502 存在しないプロジェクトのメンバー一覧", async ({ api, data }) => {
    const user = await data.createUser();

    const response = await api.get(`/api/projects/${NON_EXISTENT_ID}/members`, { headers: bearer(user) });

    expect(response.status()).toBe(404);
    expect(await response.json()).toEqual(PROJECT_NOT_FOUND);
  });

  test("API-1601 メンバー追加成功", async ({ api, data }) => {
    const owner = await data.createUser("オーナー");
    const member = await data.createUser("メンバー");
    const project = await data.createProject(owner);

    const response = await api.post(`/api/projects/${project.id}/members`, {
      headers: bearer(owner),
      data: { userId: member.id, role: "MEMBER" },
    });

    expect(response.status()).toBe(201);
    expect(await response.json()).toEqual({ projectId: project.id, userId: member.id, role: "MEMBER" });
  });

  test("API-1602 userId未指定", async ({ api, data }) => {
    const owner = await data.createUser();
    const project = await data.createProject(owner);

    const response = await api.post(`/api/projects/${project.id}/members`, {
      headers: bearer(owner),
      data: { role: "MEMBER" },
    });

    await expectValidationError(response, "userId", "ユーザーIDは必須です。");
  });

  test("API-1603 role不正値", async ({ api, data }) => {
    const owner = await data.createUser("オーナー");
    const member = await data.createUser("メンバー");
    const project = await data.createProject(owner);

    const response = await api.post(`/api/projects/${project.id}/members`, {
      headers: bearer(owner),
      data: { userId: member.id, role: "INVALID" },
    });

    await expectValidationError(response, "role", "プロジェクト内権限の値が不正です。");
  });

  test("API-1604 存在しないプロジェクトへの追加", async ({ api, data }) => {
    const user = await data.createUser();
    const member = await data.createUser("メンバー");

    const response = await api.post(`/api/projects/${NON_EXISTENT_ID}/members`, {
      headers: bearer(user),
      data: { userId: member.id, role: "MEMBER" },
    });

    expect(response.status()).toBe(404);
    expect(await response.json()).toEqual(PROJECT_NOT_FOUND);
  });

  test("API-1605 存在しないユーザーの追加", async ({ api, data }) => {
    const owner = await data.createUser();
    const project = await data.createProject(owner);

    const response = await api.post(`/api/projects/${project.id}/members`, {
      headers: bearer(owner),
      data: { userId: NON_EXISTENT_ID, role: "MEMBER" },
    });

    await expectValidationError(response, "userId", "指定されたユーザーが存在しません。");
  });

  test("API-1606 重複メンバー追加", async ({ api, data }) => {
    const owner = await data.createUser("オーナー");
    const member = await data.createUser("メンバー");
    const project = await data.createProject(owner);
    await data.addMember(project.id, owner, member);

    const response = await api.post(`/api/projects/${project.id}/members`, {
      headers: bearer(owner),
      data: { userId: member.id, role: "MEMBER" },
    });

    expect(response.status()).toBe(409);
    expect(await response.json()).toEqual({ message: "指定されたユーザーは既にプロジェクトに参加しています。" });
  });

  test("API-1701 メンバー削除成功", async ({ api, data }) => {
    const owner = await data.createUser("オーナー");
    const member = await data.createUser("メンバー");
    const project = await data.createProject(owner);
    await data.addMember(project.id, owner, member);

    const response = await api.delete(`/api/projects/${project.id}/members/${member.id}`, {
      headers: bearer(owner),
    });

    expect(response.status()).toBe(204);
    const members = await (await api.get(`/api/projects/${project.id}/members`, { headers: bearer(owner) })).json();
    expect(members.map((m: { userId: number }) => m.userId)).toEqual([owner.id]);
  });

  test("API-1702 存在しないメンバーの削除", async ({ api, data }) => {
    const owner = await data.createUser("オーナー");
    const notMember = await data.createUser("非メンバー");
    const project = await data.createProject(owner);

    const response = await api.delete(`/api/projects/${project.id}/members/${notMember.id}`, {
      headers: bearer(owner),
    });

    expect(response.status()).toBe(404);
    expect(await response.json()).toEqual({ message: "指定されたメンバーが存在しません。" });
  });

  test("API-1703 存在しないプロジェクトでの削除", async ({ api, data }) => {
    const user = await data.createUser();

    const response = await api.delete(`/api/projects/${NON_EXISTENT_ID}/members/${user.id}`, {
      headers: bearer(user),
    });

    expect(response.status()).toBe(404);
    expect(await response.json()).toEqual(PROJECT_NOT_FOUND);
  });
});
