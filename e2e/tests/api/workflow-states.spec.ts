import { bearer, expect, expectValidationError, test } from "../../support/fixtures";

test.describe("5.11 ワークフロー状態API (M2)", () => {
  test("API-2101 新規WSには既定の3状態が用意される", async ({ api, data }) => {
    const admin = await data.createUser("WS管理者");
    const workspaceId = await data.createWorkspace(admin, "ADMIN");

    const response = await api.get(`/api/workspaces/${workspaceId}/workflow-states`, {
      headers: bearer(admin),
    });

    expect(response.status()).toBe(200);
    const states = await response.json();
    expect(states.map((s: { key: string }) => s.key)).toEqual(["TODO", "IN_PROGRESS", "DONE"]);
    expect(states.find((s: { key: string }) => s.key === "TODO").isDefault).toBe(true);
  });

  test("API-2102 非所属ユーザーには 404(存在を開示しない)", async ({ api, data }) => {
    const admin = await data.createUser("WS管理者");
    const outsider = await data.createUser("部外者");
    const workspaceId = await data.createWorkspace(admin, "ADMIN");

    const response = await api.get(`/api/workspaces/${workspaceId}/workflow-states`, {
      headers: bearer(outsider),
    });

    expect(response.status()).toBe(404);
  });

  test("API-2103 WS Admin は状態を追加できる。Member は 403", async ({ api, data }) => {
    const admin = await data.createUser("WS管理者");
    const member = await data.createUser("一般メンバー");
    const workspaceId = await data.createWorkspace(admin, "ADMIN");
    await data.addWorkspaceMember(workspaceId, member, "MEMBER");

    const forbidden = await api.post(`/api/workspaces/${workspaceId}/workflow-states`, {
      headers: bearer(member),
      data: { key: "REVIEW", name: "レビュー中", category: "IN_PROGRESS" },
    });
    expect(forbidden.status()).toBe(403);

    const created = await api.post(`/api/workspaces/${workspaceId}/workflow-states`, {
      headers: bearer(admin),
      data: { key: "REVIEW", name: "レビュー中", category: "IN_PROGRESS" },
    });
    expect(created.status()).toBe(201);
    const body = await created.json();
    expect(body.key).toBe("REVIEW");
    expect(body.position).toBe(3);
  });

  test("API-2104 重複キーは 409", async ({ api, data }) => {
    const admin = await data.createUser("WS管理者");
    const workspaceId = await data.createWorkspace(admin, "ADMIN");

    const response = await api.post(`/api/workspaces/${workspaceId}/workflow-states`, {
      headers: bearer(admin),
      data: { key: "TODO", name: "別のTODO", category: "TODO" },
    });

    expect(response.status()).toBe(409);
  });

  test("API-2105 既定状態は削除できない(409)", async ({ api, data }) => {
    const admin = await data.createUser("WS管理者");
    const workspaceId = await data.createWorkspace(admin, "ADMIN");
    const states = await (await api.get(`/api/workspaces/${workspaceId}/workflow-states`, {
      headers: bearer(admin),
    })).json();
    const todo = states.find((s: { key: string }) => s.key === "TODO");

    const response = await api.delete(`/api/workspaces/${workspaceId}/workflow-states/${todo.id}`, {
      headers: bearer(admin),
    });

    expect(response.status()).toBe(409);
  });

  test("API-2106 使用中の状態は moveTo 指定で付け替えてから削除できる", async ({ api, data }) => {
    const admin = await data.createUser("WS管理者");
    const workspaceId = await data.createWorkspace(admin, "ADMIN");
    const project = await data.createProject(admin, { workspaceId });

    const review = await (await api.post(`/api/workspaces/${workspaceId}/workflow-states`, {
      headers: bearer(admin),
      data: { key: "REVIEW", name: "レビュー中", category: "IN_PROGRESS" },
    })).json();

    const task = await data.createTask(project.id, admin, { status: "REVIEW" });

    // 付け替え先なしは 409
    const inUse = await api.delete(`/api/workspaces/${workspaceId}/workflow-states/${review.id}`, {
      headers: bearer(admin),
    });
    expect(inUse.status()).toBe(409);

    // moveTo を指定すると付け替えてから削除(204)
    const deleted = await api.delete(
      `/api/workspaces/${workspaceId}/workflow-states/${review.id}?moveTo=IN_PROGRESS`,
      { headers: bearer(admin) },
    );
    expect(deleted.status()).toBe(204);

    const moved = await (await api.get(`/api/tasks/${task.id}`, { headers: bearer(admin) })).json();
    expect(moved.status).toBe("IN_PROGRESS");
  });

  test("API-2107 ワークフロー外の status でタスク作成すると 400", async ({ api, data }) => {
    const admin = await data.createUser("WS管理者");
    const workspaceId = await data.createWorkspace(admin, "ADMIN");
    const project = await data.createProject(admin, { workspaceId });

    const response = await api.post(`/api/projects/${project.id}/tasks`, {
      headers: bearer(admin),
      data: { title: "E2Eタスク", status: "NOT_A_STATE" },
    });

    await expectValidationError(response, "status", "タスク状態の値が不正です。");
  });

  test("API-2108 追加したカスタム状態は status に使える", async ({ api, data }) => {
    const admin = await data.createUser("WS管理者");
    const workspaceId = await data.createWorkspace(admin, "ADMIN");
    const project = await data.createProject(admin, { workspaceId });

    await api.post(`/api/workspaces/${workspaceId}/workflow-states`, {
      headers: bearer(admin),
      data: { key: "REVIEW", name: "レビュー中", category: "IN_PROGRESS" },
    });

    const response = await api.post(`/api/projects/${project.id}/tasks`, {
      headers: bearer(admin),
      data: { title: "E2Eタスク", status: "REVIEW" },
    });

    expect(response.status()).toBe(201);
    expect((await response.json()).status).toBe("REVIEW");
  });

  test("API-2109 WS Admin は状態を改名・並べ替えできる。Member は 403", async ({ api, data }) => {
    const admin = await data.createUser("WS管理者");
    const member = await data.createUser("一般メンバー");
    const workspaceId = await data.createWorkspace(admin, "ADMIN");
    await data.addWorkspaceMember(workspaceId, member, "MEMBER");
    const states = await (await api.get(`/api/workspaces/${workspaceId}/workflow-states`, {
      headers: bearer(admin),
    })).json();
    const done = states.find((s: { key: string }) => s.key === "DONE");

    // Member は変更できない
    const forbidden = await api.patch(`/api/workspaces/${workspaceId}/workflow-states/${done.id}`, {
      headers: bearer(member),
      data: { name: "クローズ", category: "DONE" },
    });
    expect(forbidden.status()).toBe(403);

    // Admin は改名＋先頭(position=0)へ並べ替え
    const updated = await api.patch(`/api/workspaces/${workspaceId}/workflow-states/${done.id}`, {
      headers: bearer(admin),
      data: { name: "クローズ", category: "DONE", position: 0 },
    });
    expect(updated.status()).toBe(200);

    const reordered = await (await api.get(`/api/workspaces/${workspaceId}/workflow-states`, {
      headers: bearer(admin),
    })).json();
    expect(reordered[0].key).toBe("DONE");
    expect(reordered[0].name).toBe("クローズ");
  });
});
