import { bearer, expect, test } from "../../support/fixtures";

test.describe("5.15 保存ビューAPI (M2)", () => {
  test("API-2501 個人ビューは本人のみ、共有ビューは同一WSの所属者に見える", async ({ api, data }) => {
    const owner = await data.createUser("作成者");
    const other = await data.createUser("別メンバー");
    const workspaceId = await data.createWorkspace(owner, "ADMIN");
    await data.addWorkspaceMember(workspaceId, other, "MEMBER");

    await api.post(`/api/workspaces/${workspaceId}/views`, {
      headers: bearer(owner),
      data: { name: "個人用", viewType: "LIST", isShared: false, filters: null },
    });
    await api.post(`/api/workspaces/${workspaceId}/views`, {
      headers: bearer(owner),
      data: { name: "共有用", viewType: "LIST", isShared: true, filters: null },
    });

    const forOther = await (await api.get(`/api/workspaces/${workspaceId}/views`, {
      headers: bearer(other),
    })).json();
    expect(forOther.map((v: { name: string }) => v.name)).toEqual(["共有用"]);
    expect(forOther[0].isOwner).toBe(false);

    const forOwner = await (await api.get(`/api/workspaces/${workspaceId}/views`, {
      headers: bearer(owner),
    })).json();
    expect(forOwner).toHaveLength(2);
  });

  test("API-2502 非所属ユーザーには 404", async ({ api, data }) => {
    const owner = await data.createUser("作成者");
    const outsider = await data.createUser("部外者");
    const workspaceId = await data.createWorkspace(owner, "ADMIN");

    const response = await api.get(`/api/workspaces/${workspaceId}/views`, { headers: bearer(outsider) });
    expect(response.status()).toBe(404);
  });

  test("API-2503 絞り込み条件を保存して読み出せる", async ({ api, data }) => {
    const owner = await data.createUser("作成者");
    const workspaceId = await data.createWorkspace(owner, "ADMIN");

    const created = await api.post(`/api/workspaces/${workspaceId}/views`, {
      headers: bearer(owner),
      data: {
        name: "作業中",
        viewType: "BOARD",
        isShared: false,
        filters: { status: ["TODO", "IN_PROGRESS"], assigneeId: "me", priority: ["HIGH"], keyword: "api", sort: "-updatedAt" },
      },
    });
    expect(created.status()).toBe(201);
    const view = await created.json();
    expect(view.viewType).toBe("BOARD");
    expect(view.filters.status).toEqual(["TODO", "IN_PROGRESS"]);
    expect(view.filters.assigneeId).toBe("me");
    expect(view.filters.keyword).toBe("api");
    expect(view.filters.sort).toBe("-updatedAt");
  });

  test("API-2504 他人の個人ビューは削除できない(403)。本人は削除できる", async ({ api, data }) => {
    const owner = await data.createUser("作成者");
    const other = await data.createUser("別メンバー");
    const workspaceId = await data.createWorkspace(owner, "ADMIN");
    await data.addWorkspaceMember(workspaceId, other, "MEMBER");

    const view = await (await api.post(`/api/workspaces/${workspaceId}/views`, {
      headers: bearer(owner),
      data: { name: "共有", viewType: "LIST", isShared: true, filters: null },
    })).json();

    const forbidden = await api.delete(`/api/workspaces/${workspaceId}/views/${view.id}`, {
      headers: bearer(other),
    });
    expect(forbidden.status()).toBe(403);

    const ok = await api.delete(`/api/workspaces/${workspaceId}/views/${view.id}`, {
      headers: bearer(owner),
    });
    expect(ok.status()).toBe(204);
  });
});
