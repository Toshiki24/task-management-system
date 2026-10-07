import { bearer, expect, test } from "../../support/fixtures";

test.describe("5.14 タスク検索・絞り込み・並べ替え (M2)", () => {
  test("API-2401 status で絞り込める", async ({ api, data }) => {
    const owner = await data.createUser("オーナー");
    const project = await data.createProject(owner);
    await data.createTask(project.id, owner, { status: "TODO" });
    await data.createTask(project.id, owner, { status: "IN_PROGRESS" });

    const response = await api.get(`/api/projects/${project.id}/tasks?status=IN_PROGRESS`, {
      headers: bearer(owner),
    });
    expect(response.status()).toBe(200);
    const tasks = await response.json();
    expect(tasks).toHaveLength(1);
    expect(tasks[0].status).toBe("IN_PROGRESS");
  });

  test("API-2402 assigneeId=me / none で絞り込める", async ({ api, data }) => {
    const owner = await data.createUser("オーナー");
    const project = await data.createProject(owner);
    await data.createTask(project.id, owner, { assigneeId: owner.id });
    await data.createTask(project.id, owner, { assigneeId: null });

    const mine = await (await api.get(`/api/projects/${project.id}/tasks?assigneeId=me`, {
      headers: bearer(owner),
    })).json();
    expect(mine).toHaveLength(1);
    expect(mine[0].assigneeId).toBe(owner.id);

    const none = await (await api.get(`/api/projects/${project.id}/tasks?assigneeId=none`, {
      headers: bearer(owner),
    })).json();
    expect(none).toHaveLength(1);
    expect(none[0].assigneeId).toBeNull();
  });

  test("API-2403 keyword でタイトル・説明を部分一致検索できる", async ({ api, data }) => {
    const owner = await data.createUser("オーナー");
    const project = await data.createProject(owner);
    await data.createTask(project.id, owner, { title: "ログイン画面の改修" });
    await data.createTask(project.id, owner, { title: "まったく別のタスク" });

    const response = await api.get(`/api/projects/${project.id}/tasks?keyword=${encodeURIComponent("ログイン")}`, {
      headers: bearer(owner),
    });
    const tasks = await response.json();
    expect(tasks).toHaveLength(1);
    expect(tasks[0].title).toContain("ログイン");
  });

  test("API-2404 label で絞り込める", async ({ api, data }) => {
    const admin = await data.createUser("WS管理者");
    const workspaceId = await data.createWorkspace(admin, "ADMIN");
    const project = await data.createProject(admin, { workspaceId });
    const label = await (await api.post(`/api/workspaces/${workspaceId}/labels`, {
      headers: bearer(admin),
      data: { name: "bug", color: null },
    })).json();
    await api.post(`/api/projects/${project.id}/tasks`, {
      headers: bearer(admin),
      data: { title: "ラベル付き", labelIds: [label.id] },
    });
    await data.createTask(project.id, admin, { title: "ラベルなし" });

    const response = await api.get(`/api/projects/${project.id}/tasks?labelId=${label.id}`, {
      headers: bearer(admin),
    });
    const tasks = await response.json();
    expect(tasks).toHaveLength(1);
    expect(tasks[0].title).toBe("ラベル付き");
  });

  test("API-2405 sort=title / -title で並べ替えられる", async ({ api, data }) => {
    const owner = await data.createUser("オーナー");
    const project = await data.createProject(owner);
    await data.createTask(project.id, owner, { title: "B-task" });
    await data.createTask(project.id, owner, { title: "A-task" });
    await data.createTask(project.id, owner, { title: "C-task" });

    const asc = await (await api.get(`/api/projects/${project.id}/tasks?sort=title`, {
      headers: bearer(owner),
    })).json();
    expect(asc.map((t: { title: string }) => t.title)).toEqual(["A-task", "B-task", "C-task"]);

    const desc = await (await api.get(`/api/projects/${project.id}/tasks?sort=-title`, {
      headers: bearer(owner),
    })).json();
    expect(desc.map((t: { title: string }) => t.title)).toEqual(["C-task", "B-task", "A-task"]);
  });
});
