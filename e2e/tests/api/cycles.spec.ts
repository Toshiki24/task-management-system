import { bearer, expect, expectValidationError, test } from "../../support/fixtures";

test.describe("5.24 サイクルAPI (M3)", () => {
  test("API-3401 OWNER/WS Admin は作成でき、一般メンバーは 403", async ({ api, data }) => {
    const owner = await data.createUser("オーナー");
    const member = await data.createUser("メンバー");
    const project = await data.createProject(owner);
    await data.addMember(project.id, owner, member);

    const forbidden = await api.post(`/api/projects/${project.id}/cycles`, {
      headers: bearer(member),
      data: { name: "Sprint 1" },
    });
    expect(forbidden.status()).toBe(403);

    const created = await api.post(`/api/projects/${project.id}/cycles`, {
      headers: bearer(owner),
      data: { name: "Sprint 1" },
    });
    expect(created.status()).toBe(201);
    expect((await created.json()).name).toBe("Sprint 1");
  });

  test("API-3402 一覧は進捗(完了/全数/見積合計)を返す", async ({ api, data }) => {
    const owner = await data.createUser("オーナー");
    const project = await data.createProject(owner);
    const cycle = await (await api.post(`/api/projects/${project.id}/cycles`, {
      headers: bearer(owner), data: { name: "Sprint 1" },
    })).json();
    const done = await data.createTask(project.id, owner, { status: "DONE", estimatePoints: 3 });
    const todo = await data.createTask(project.id, owner, { status: "TODO", estimatePoints: 5 });
    for (const t of [done, todo]) {
      await api.patch(`/api/projects/${project.id}/tasks/${t.id}/cycle`, {
        headers: bearer(owner), data: { cycleId: cycle.id },
      });
    }

    const list = await (await api.get(`/api/projects/${project.id}/cycles`, { headers: bearer(owner) })).json();
    expect(list[0].progress).toEqual({ done: 1, total: 2, points: 8 });
  });

  test("API-3403 タスクの割り当て・解除、別PJサイクルは 400", async ({ api, data }) => {
    const owner = await data.createUser("オーナー");
    const project = await data.createProject(owner);
    const cycle = await (await api.post(`/api/projects/${project.id}/cycles`, {
      headers: bearer(owner), data: { name: "Sprint 1" },
    })).json();
    const task = await data.createTask(project.id, owner);

    // 割り当て
    const assign = await api.patch(`/api/projects/${project.id}/tasks/${task.id}/cycle`, {
      headers: bearer(owner), data: { cycleId: cycle.id },
    });
    expect(assign.status()).toBe(204);
    expect((await (await api.get(`/api/tasks/${task.id}`, { headers: bearer(owner) })).json()).cycleId).toBe(cycle.id);

    // バックログへ解除
    await api.patch(`/api/projects/${project.id}/tasks/${task.id}/cycle`, {
      headers: bearer(owner), data: { cycleId: null },
    });
    expect((await (await api.get(`/api/tasks/${task.id}`, { headers: bearer(owner) })).json()).cycleId).toBeNull();

    // 別プロジェクトのサイクルは不正
    const otherProject = await data.createProject(owner);
    const otherCycle = await (await api.post(`/api/projects/${otherProject.id}/cycles`, {
      headers: bearer(owner), data: { name: "別Sprint" },
    })).json();
    const response = await api.patch(`/api/projects/${project.id}/tasks/${task.id}/cycle`, {
      headers: bearer(owner), data: { cycleId: otherCycle.id },
    });
    await expectValidationError(response, "cycleId");
  });

  test("API-3404 削除でタスクはバックログへ戻る", async ({ api, data }) => {
    const owner = await data.createUser("オーナー");
    const project = await data.createProject(owner);
    const cycle = await (await api.post(`/api/projects/${project.id}/cycles`, {
      headers: bearer(owner), data: { name: "Sprint 1" },
    })).json();
    const task = await data.createTask(project.id, owner);
    await api.patch(`/api/projects/${project.id}/tasks/${task.id}/cycle`, {
      headers: bearer(owner), data: { cycleId: cycle.id },
    });

    const del = await api.delete(`/api/cycles/${cycle.id}`, { headers: bearer(owner) });
    expect(del.status()).toBe(204);
    expect((await (await api.get(`/api/tasks/${task.id}`, { headers: bearer(owner) })).json()).cycleId).toBeNull();
  });

  test("API-3405 非所属ユーザーには 404(存在を開示しない)", async ({ api, data }) => {
    const owner = await data.createUser("オーナー");
    const outsider = await data.createUser("部外者");
    const project = await data.createProject(owner);

    const response = await api.get(`/api/projects/${project.id}/cycles`, { headers: bearer(outsider) });
    expect(response.status()).toBe(404);
  });
});
