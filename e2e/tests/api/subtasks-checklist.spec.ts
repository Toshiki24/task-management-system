import { bearer, expect, expectValidationError, test } from "../../support/fixtures";

test.describe("5.17 サブタスク・チェックリストAPI (M2)", () => {
  test("API-2701 サブタスクを作成でき、親の進捗に反映される", async ({ api, data }) => {
    const owner = await data.createUser("オーナー");
    const project = await data.createProject(owner);
    const parent = await data.createTask(project.id, owner);

    const child = await api.post(`/api/projects/${project.id}/tasks`, {
      headers: bearer(owner),
      data: { title: "子タスク", status: "DONE", parentTaskId: parent.id },
    });
    expect(child.status()).toBe(201);
    expect((await child.json()).parentTaskId).toBe(parent.id);

    const reloadedParent = await (await api.get(`/api/tasks/${parent.id}`, { headers: bearer(owner) })).json();
    expect(reloadedParent.subtaskProgress).toEqual({ done: 1, total: 1 });
  });

  test("API-2702 別プロジェクトの親タスク指定は 400", async ({ api, data }) => {
    const owner = await data.createUser("オーナー");
    const project = await data.createProject(owner);
    const otherProject = await data.createProject(owner);
    const foreignParent = await data.createTask(otherProject.id, owner);

    const response = await api.post(`/api/projects/${project.id}/tasks`, {
      headers: bearer(owner),
      data: { title: "子", parentTaskId: foreignParent.id },
    });
    await expectValidationError(response, "parentTaskId");
  });

  test("API-2703 サブタスクをさらに親に指定する(2階層)と 400", async ({ api, data }) => {
    const owner = await data.createUser("オーナー");
    const project = await data.createProject(owner);
    const parent = await data.createTask(project.id, owner);
    const child = await (await api.post(`/api/projects/${project.id}/tasks`, {
      headers: bearer(owner),
      data: { title: "子", parentTaskId: parent.id },
    })).json();

    const response = await api.post(`/api/projects/${project.id}/tasks`, {
      headers: bearer(owner),
      data: { title: "孫", parentTaskId: child.id },
    });
    await expectValidationError(response, "parentTaskId");
  });

  test("API-2704 チェックリストの作成・一覧・トグル・削除", async ({ api, data }) => {
    const owner = await data.createUser("オーナー");
    const project = await data.createProject(owner);
    const task = await data.createTask(project.id, owner);

    const created = await api.post(`/api/tasks/${task.id}/checklist`, {
      headers: bearer(owner),
      data: { content: "手順1" },
    });
    expect(created.status()).toBe(201);
    const item = await created.json();
    expect(item.isDone).toBe(false);

    const toggled = await api.patch(`/api/tasks/${task.id}/checklist/${item.id}`, {
      headers: bearer(owner),
      data: { content: "手順1", isDone: true },
    });
    expect(toggled.status()).toBe(200);
    expect((await toggled.json()).isDone).toBe(true);

    const list = await (await api.get(`/api/tasks/${task.id}/checklist`, { headers: bearer(owner) })).json();
    expect(list).toHaveLength(1);

    const deleted = await api.delete(`/api/tasks/${task.id}/checklist/${item.id}`, { headers: bearer(owner) });
    expect(deleted.status()).toBe(204);
    expect(await (await api.get(`/api/tasks/${task.id}/checklist`, { headers: bearer(owner) })).json()).toEqual([]);
  });

  test("API-2705 Viewer はチェックリストを追加できない(403)", async ({ api, data }) => {
    const owner = await data.createUser("オーナー");
    const viewer = await data.createUser("閲覧者");
    const workspaceId = await data.createWorkspace(owner, "ADMIN");
    await data.addWorkspaceMember(workspaceId, viewer, "VIEWER");
    const project = await data.createProject(owner, { workspaceId });
    const task = await data.createTask(project.id, owner);

    const response = await api.post(`/api/tasks/${task.id}/checklist`, {
      headers: bearer(viewer),
      data: { content: "x" },
    });
    expect(response.status()).toBe(403);
  });
});
