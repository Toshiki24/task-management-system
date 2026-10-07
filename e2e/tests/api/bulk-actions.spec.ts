import { bearer, expect, expectValidationError, test } from "../../support/fixtures";

test.describe("5.19 一括操作API (M2)", () => {
  test("API-2901 一括で状態を変更できる", async ({ api, data }) => {
    const owner = await data.createUser("オーナー");
    const project = await data.createProject(owner);
    const t1 = await data.createTask(project.id, owner);
    const t2 = await data.createTask(project.id, owner);

    const response = await api.patch(`/api/projects/${project.id}/tasks/bulk`, {
      headers: bearer(owner),
      data: { taskIds: [t1.id, t2.id], status: "DONE" },
    });
    expect(response.status()).toBe(200);
    expect((await response.json()).updated).toBe(2);

    for (const id of [t1.id, t2.id]) {
      const task = await (await api.get(`/api/tasks/${id}`, { headers: bearer(owner) })).json();
      expect(task.status).toBe("DONE");
    }
  });

  test("API-2902 一括で担当を設定・未割り当てにできる", async ({ api, data }) => {
    const owner = await data.createUser("オーナー");
    const member = await data.createUser("担当者");
    const project = await data.createProject(owner);
    await data.addMember(project.id, owner, member);
    const t1 = await data.createTask(project.id, owner, { assigneeId: owner.id });

    await api.patch(`/api/projects/${project.id}/tasks/bulk`, {
      headers: bearer(owner),
      data: { taskIds: [t1.id], setAssignee: true, assigneeId: member.id },
    });
    expect((await (await api.get(`/api/tasks/${t1.id}`, { headers: bearer(owner) })).json()).assigneeId).toBe(member.id);

    await api.patch(`/api/projects/${project.id}/tasks/bulk`, {
      headers: bearer(owner),
      data: { taskIds: [t1.id], setAssignee: true, assigneeId: null },
    });
    expect((await (await api.get(`/api/tasks/${t1.id}`, { headers: bearer(owner) })).json()).assigneeId).toBeNull();
  });

  test("API-2903 一括でラベルを付与・除去できる", async ({ api, data }) => {
    const owner = await data.createUser("オーナー");
    const workspaceId = await data.createWorkspace(owner, "ADMIN");
    const project = await data.createProject(owner, { workspaceId });
    const label1 = await (await api.post(`/api/workspaces/${workspaceId}/labels`, {
      headers: bearer(owner), data: { name: "bug", color: "#ff0000" },
    })).json();
    const label2 = await (await api.post(`/api/workspaces/${workspaceId}/labels`, {
      headers: bearer(owner), data: { name: "urgent", color: "#00ff00" },
    })).json();
    const t1 = await data.createTask(project.id, owner);

    // 付与
    await api.patch(`/api/projects/${project.id}/tasks/bulk`, {
      headers: bearer(owner),
      data: { taskIds: [t1.id], addLabelIds: [label1.id, label2.id] },
    });
    let task = await (await api.get(`/api/tasks/${t1.id}`, { headers: bearer(owner) })).json();
    expect(task.labels.map((l: { id: number }) => l.id).sort()).toEqual([label1.id, label2.id].sort());

    // label1 を除去
    await api.patch(`/api/projects/${project.id}/tasks/bulk`, {
      headers: bearer(owner),
      data: { taskIds: [t1.id], removeLabelIds: [label1.id] },
    });
    task = await (await api.get(`/api/tasks/${t1.id}`, { headers: bearer(owner) })).json();
    expect(task.labels.map((l: { id: number }) => l.id)).toEqual([label2.id]);
  });

  test("API-2904 別プロジェクトのタスク ID が混ざると 400(何も変更しない)", async ({ api, data }) => {
    const owner = await data.createUser("オーナー");
    const project = await data.createProject(owner);
    const otherProject = await data.createProject(owner);
    const t1 = await data.createTask(project.id, owner);
    const foreign = await data.createTask(otherProject.id, owner);

    const response = await api.patch(`/api/projects/${project.id}/tasks/bulk`, {
      headers: bearer(owner),
      data: { taskIds: [t1.id, foreign.id], status: "DONE" },
    });
    await expectValidationError(response, "taskIds");
    // t1 は変更されていない
    expect((await (await api.get(`/api/tasks/${t1.id}`, { headers: bearer(owner) })).json()).status).not.toBe("DONE");
  });

  test("API-2905 不正な状態は 400", async ({ api, data }) => {
    const owner = await data.createUser("オーナー");
    const project = await data.createProject(owner);
    const t1 = await data.createTask(project.id, owner);

    const response = await api.patch(`/api/projects/${project.id}/tasks/bulk`, {
      headers: bearer(owner),
      data: { taskIds: [t1.id], status: "NOT_A_STATE" },
    });
    await expectValidationError(response, "status");
  });

  test("API-2906 Viewer は一括更新できない(403)", async ({ api, data }) => {
    const owner = await data.createUser("オーナー");
    const viewer = await data.createUser("閲覧者");
    const workspaceId = await data.createWorkspace(owner, "ADMIN");
    await data.addWorkspaceMember(workspaceId, viewer, "VIEWER");
    const project = await data.createProject(owner, { workspaceId });
    const t1 = await data.createTask(project.id, owner);

    const response = await api.patch(`/api/projects/${project.id}/tasks/bulk`, {
      headers: bearer(viewer),
      data: { taskIds: [t1.id], status: "DONE" },
    });
    expect(response.status()).toBe(403);
  });

  test("API-2907 非所属ユーザーには 404(存在を開示しない)", async ({ api, data }) => {
    const owner = await data.createUser("オーナー");
    const outsider = await data.createUser("部外者");
    const project = await data.createProject(owner);
    const t1 = await data.createTask(project.id, owner);

    const response = await api.patch(`/api/projects/${project.id}/tasks/bulk`, {
      headers: bearer(outsider),
      data: { taskIds: [t1.id], status: "DONE" },
    });
    expect(response.status()).toBe(404);
  });
});
