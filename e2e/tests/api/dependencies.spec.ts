import { bearer, expect, expectValidationError, test } from "../../support/fixtures";

test.describe("5.18 タスク依存API (M2)", () => {
  test("API-2801 BLOCKED_BY/BLOCKS を追加すると両タスクから対称に見える", async ({ api, data }) => {
    const owner = await data.createUser("オーナー");
    const project = await data.createProject(owner);
    const a = await data.createTask(project.id, owner, { title: "A" });
    const b = await data.createTask(project.id, owner, { title: "B" });

    // A は B に待たされる(A blockedBy B)
    const added = await api.post(`/api/tasks/${a.id}/dependencies`, {
      headers: bearer(owner),
      data: { taskId: b.id, relation: "BLOCKED_BY" },
    });
    expect(added.status()).toBe(201);
    expect((await added.json()).taskId).toBe(b.id);

    const aDeps = await (await api.get(`/api/tasks/${a.id}/dependencies`, { headers: bearer(owner) })).json();
    expect(aDeps.blockedBy.map((d: { taskId: number }) => d.taskId)).toEqual([b.id]);
    expect(aDeps.blocking).toEqual([]);

    // 相手 B からは blocking として見える
    const bDeps = await (await api.get(`/api/tasks/${b.id}/dependencies`, { headers: bearer(owner) })).json();
    expect(bDeps.blocking.map((d: { taskId: number }) => d.taskId)).toEqual([a.id]);
    expect(bDeps.blockedBy).toEqual([]);
  });

  test("API-2802 自己依存は 400", async ({ api, data }) => {
    const owner = await data.createUser("オーナー");
    const project = await data.createProject(owner);
    const a = await data.createTask(project.id, owner);

    const response = await api.post(`/api/tasks/${a.id}/dependencies`, {
      headers: bearer(owner),
      data: { taskId: a.id, relation: "BLOCKED_BY" },
    });
    await expectValidationError(response, "taskId");
  });

  test("API-2803 別プロジェクトのタスク指定は 400", async ({ api, data }) => {
    const owner = await data.createUser("オーナー");
    const project = await data.createProject(owner);
    const otherProject = await data.createProject(owner);
    const a = await data.createTask(project.id, owner);
    const foreign = await data.createTask(otherProject.id, owner);

    const response = await api.post(`/api/tasks/${a.id}/dependencies`, {
      headers: bearer(owner),
      data: { taskId: foreign.id, relation: "BLOCKED_BY" },
    });
    await expectValidationError(response, "taskId");
  });

  test("API-2804 同じ依存の二重登録は 409", async ({ api, data }) => {
    const owner = await data.createUser("オーナー");
    const project = await data.createProject(owner);
    const a = await data.createTask(project.id, owner);
    const b = await data.createTask(project.id, owner);

    const first = await api.post(`/api/tasks/${a.id}/dependencies`, {
      headers: bearer(owner),
      data: { taskId: b.id, relation: "BLOCKED_BY" },
    });
    expect(first.status()).toBe(201);

    // 同じ辺(B→A)を逆向きの指定でもう一度 = B BLOCKS A
    const dup = await api.post(`/api/tasks/${b.id}/dependencies`, {
      headers: bearer(owner),
      data: { taskId: a.id, relation: "BLOCKS" },
    });
    expect(dup.status()).toBe(409);
  });

  test("API-2805 循環する依存は 409", async ({ api, data }) => {
    const owner = await data.createUser("オーナー");
    const project = await data.createProject(owner);
    const a = await data.createTask(project.id, owner);
    const b = await data.createTask(project.id, owner);
    const c = await data.createTask(project.id, owner);

    // A→B, B→C
    expect((await api.post(`/api/tasks/${a.id}/dependencies`, {
      headers: bearer(owner), data: { taskId: b.id, relation: "BLOCKS" },
    })).status()).toBe(201);
    expect((await api.post(`/api/tasks/${b.id}/dependencies`, {
      headers: bearer(owner), data: { taskId: c.id, relation: "BLOCKS" },
    })).status()).toBe(201);

    // C→A は循環
    const cycle = await api.post(`/api/tasks/${c.id}/dependencies`, {
      headers: bearer(owner),
      data: { taskId: a.id, relation: "BLOCKS" },
    });
    expect(cycle.status()).toBe(409);
  });

  test("API-2806 完了カテゴリのブロッカーは isClosed=true", async ({ api, data }) => {
    const owner = await data.createUser("オーナー");
    const project = await data.createProject(owner);
    const a = await data.createTask(project.id, owner);
    const blocker = await data.createTask(project.id, owner, { status: "DONE" });

    await api.post(`/api/tasks/${a.id}/dependencies`, {
      headers: bearer(owner),
      data: { taskId: blocker.id, relation: "BLOCKED_BY" },
    });

    const deps = await (await api.get(`/api/tasks/${a.id}/dependencies`, { headers: bearer(owner) })).json();
    expect(deps.blockedBy[0].isClosed).toBe(true);
  });

  test("API-2807 依存を削除できる。無関係タスクからの削除は 404", async ({ api, data }) => {
    const owner = await data.createUser("オーナー");
    const project = await data.createProject(owner);
    const a = await data.createTask(project.id, owner);
    const b = await data.createTask(project.id, owner);
    const c = await data.createTask(project.id, owner);

    const edge = await (await api.post(`/api/tasks/${a.id}/dependencies`, {
      headers: bearer(owner),
      data: { taskId: b.id, relation: "BLOCKED_BY" },
    })).json();

    // C は辺に関与していない
    const wrong = await api.delete(`/api/tasks/${c.id}/dependencies/${edge.dependencyId}`, { headers: bearer(owner) });
    expect(wrong.status()).toBe(404);

    const deleted = await api.delete(`/api/tasks/${a.id}/dependencies/${edge.dependencyId}`, { headers: bearer(owner) });
    expect(deleted.status()).toBe(204);

    const aDeps = await (await api.get(`/api/tasks/${a.id}/dependencies`, { headers: bearer(owner) })).json();
    expect(aDeps.blockedBy).toEqual([]);
  });

  test("API-2808 Viewer は依存を追加できない(403)", async ({ api, data }) => {
    const owner = await data.createUser("オーナー");
    const viewer = await data.createUser("閲覧者");
    const workspaceId = await data.createWorkspace(owner, "ADMIN");
    await data.addWorkspaceMember(workspaceId, viewer, "VIEWER");
    const project = await data.createProject(owner, { workspaceId });
    const a = await data.createTask(project.id, owner);
    const b = await data.createTask(project.id, owner);

    const response = await api.post(`/api/tasks/${a.id}/dependencies`, {
      headers: bearer(viewer),
      data: { taskId: b.id, relation: "BLOCKED_BY" },
    });
    expect(response.status()).toBe(403);
  });

  test("API-2809 非所属ユーザーには 404(存在を開示しない)", async ({ api, data }) => {
    const owner = await data.createUser("オーナー");
    const outsider = await data.createUser("部外者");
    const project = await data.createProject(owner);
    const a = await data.createTask(project.id, owner);

    const response = await api.get(`/api/tasks/${a.id}/dependencies`, { headers: bearer(outsider) });
    expect(response.status()).toBe(404);
  });
});
