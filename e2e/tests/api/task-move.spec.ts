import { bearer, expect, expectValidationError, test } from "../../support/fixtures";

test.describe("5.12 カンバン カード移動API (M2)", () => {
  test("API-2201 移動で status が変わる", async ({ api, data }) => {
    const owner = await data.createUser("オーナー");
    const project = await data.createProject(owner);
    const task = await data.createTask(project.id, owner); // 既定 TODO

    const response = await api.patch(`/api/projects/${project.id}/tasks/${task.id}/move`, {
      headers: bearer(owner),
      data: { toStatus: "IN_PROGRESS", beforeTaskId: null },
    });

    expect(response.status()).toBe(200);
    expect((await response.json()).status).toBe("IN_PROGRESS");

    const reloaded = await (await api.get(`/api/tasks/${task.id}`, { headers: bearer(owner) })).json();
    expect(reloaded.status).toBe("IN_PROGRESS");
  });

  test("API-2202 ワークフロー外の移動先は 400", async ({ api, data }) => {
    const owner = await data.createUser("オーナー");
    const project = await data.createProject(owner);
    const task = await data.createTask(project.id, owner);

    const response = await api.patch(`/api/projects/${project.id}/tasks/${task.id}/move`, {
      headers: bearer(owner),
      data: { toStatus: "NOPE", beforeTaskId: null },
    });

    await expectValidationError(response, "toStatus", "タスク状態の値が不正です。");
  });

  test("API-2203 Viewer はカード移動できない(403)", async ({ api, data }) => {
    const owner = await data.createUser("オーナー");
    const viewer = await data.createUser("閲覧者");
    const workspaceId = await data.createWorkspace(owner, "ADMIN");
    await data.addWorkspaceMember(workspaceId, viewer, "VIEWER");
    const project = await data.createProject(owner, { workspaceId });
    const task = await data.createTask(project.id, owner);

    const response = await api.patch(`/api/projects/${project.id}/tasks/${task.id}/move`, {
      headers: bearer(viewer),
      data: { toStatus: "DONE", beforeTaskId: null },
    });

    expect(response.status()).toBe(403);
  });

  test("API-2204 beforeTaskId 指定で列内の並び順が変わる", async ({ api, data }) => {
    const owner = await data.createUser("オーナー");
    const project = await data.createProject(owner);
    const a = await data.createTask(project.id, owner); // TODO, pos 0
    const b = await data.createTask(project.id, owner); // TODO, pos 1
    const c = await data.createTask(project.id, owner); // TODO, pos 2

    // C を A の直前へ
    const moved = await api.patch(`/api/projects/${project.id}/tasks/${c.id}/move`, {
      headers: bearer(owner),
      data: { toStatus: "TODO", beforeTaskId: a.id },
    });
    expect(moved.status()).toBe(200);

    const tasks = await (await api.get(`/api/projects/${project.id}/tasks`, { headers: bearer(owner) })).json();
    const pos = (id: number) => tasks.find((t: { id: number }) => t.id === id).boardPosition;
    // 並びは C, A, B
    expect(pos(c.id)).toBeLessThan(pos(a.id));
    expect(pos(a.id)).toBeLessThan(pos(b.id));
  });
});
