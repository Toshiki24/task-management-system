import { bearer, expect, expectValidationError, test } from "../../support/fixtures";

test.describe("5.13 ラベルAPI (M2)", () => {
  test("API-2301 WS Admin はラベルを作成・一覧できる", async ({ api, data }) => {
    const admin = await data.createUser("WS管理者");
    const workspaceId = await data.createWorkspace(admin, "ADMIN");

    const created = await api.post(`/api/workspaces/${workspaceId}/labels`, {
      headers: bearer(admin),
      data: { name: "bug", color: "#ff0000" },
    });
    expect(created.status()).toBe(201);
    expect((await created.json()).name).toBe("bug");

    const list = await api.get(`/api/workspaces/${workspaceId}/labels`, { headers: bearer(admin) });
    expect(list.status()).toBe(200);
    expect((await list.json()).map((l: { name: string }) => l.name)).toContain("bug");
  });

  test("API-2302 一般メンバーはラベルを作成できない(403)", async ({ api, data }) => {
    const admin = await data.createUser("WS管理者");
    const member = await data.createUser("一般");
    const workspaceId = await data.createWorkspace(admin, "ADMIN");
    await data.addWorkspaceMember(workspaceId, member, "MEMBER");

    const response = await api.post(`/api/workspaces/${workspaceId}/labels`, {
      headers: bearer(member),
      data: { name: "feature", color: null },
    });
    expect(response.status()).toBe(403);
  });

  test("API-2303 非所属ユーザーには一覧が 404", async ({ api, data }) => {
    const admin = await data.createUser("WS管理者");
    const outsider = await data.createUser("部外者");
    const workspaceId = await data.createWorkspace(admin, "ADMIN");

    const response = await api.get(`/api/workspaces/${workspaceId}/labels`, { headers: bearer(outsider) });
    expect(response.status()).toBe(404);
  });

  test("API-2304 重複名は 409", async ({ api, data }) => {
    const admin = await data.createUser("WS管理者");
    const workspaceId = await data.createWorkspace(admin, "ADMIN");
    await api.post(`/api/workspaces/${workspaceId}/labels`, {
      headers: bearer(admin),
      data: { name: "dup", color: null },
    });

    const again = await api.post(`/api/workspaces/${workspaceId}/labels`, {
      headers: bearer(admin),
      data: { name: "dup", color: null },
    });
    expect(again.status()).toBe(409);
  });

  test("API-2305 タスクにラベルを付与でき、削除で連動して外れる", async ({ api, data }) => {
    const admin = await data.createUser("WS管理者");
    const workspaceId = await data.createWorkspace(admin, "ADMIN");
    const project = await data.createProject(admin, { workspaceId });
    const label = await (await api.post(`/api/workspaces/${workspaceId}/labels`, {
      headers: bearer(admin),
      data: { name: "urgent", color: "#f00" },
    })).json();

    const created = await api.post(`/api/projects/${project.id}/tasks`, {
      headers: bearer(admin),
      data: { title: "ラベル付き", labelIds: [label.id] },
    });
    expect(created.status()).toBe(201);
    const task = await created.json();
    expect(task.labels.map((l: { name: string }) => l.name)).toEqual(["urgent"]);
    expect(task.estimatePoints).toBeNull();

    // ラベル削除後はタスクから外れる
    const del = await api.delete(`/api/workspaces/${workspaceId}/labels/${label.id}`, { headers: bearer(admin) });
    expect(del.status()).toBe(204);

    const reloaded = await (await api.get(`/api/tasks/${task.id}`, { headers: bearer(admin) })).json();
    expect(reloaded.labels).toEqual([]);
  });

  test("API-2306 存在しないラベルIDの付与は 400", async ({ api, data }) => {
    const admin = await data.createUser("WS管理者");
    const workspaceId = await data.createWorkspace(admin, "ADMIN");
    const project = await data.createProject(admin, { workspaceId });

    const response = await api.post(`/api/projects/${project.id}/tasks`, {
      headers: bearer(admin),
      data: { title: "T", labelIds: [999999999] },
    });
    await expectValidationError(response, "labelIds", "指定されたラベルが存在しません。");
  });
});
