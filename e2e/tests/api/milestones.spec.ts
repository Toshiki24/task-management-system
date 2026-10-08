import { bearer, expect, expectValidationError, test } from "../../support/fixtures";

test.describe("5.25 マイルストーンAPI (M3)", () => {
  test("API-3501 OWNER/WS Admin は作成でき、一般メンバーは 403", async ({ api, data }) => {
    const owner = await data.createUser("オーナー");
    const member = await data.createUser("メンバー");
    const project = await data.createProject(owner);
    await data.addMember(project.id, owner, member);

    const forbidden = await api.post(`/api/projects/${project.id}/milestones`, {
      headers: bearer(member),
      data: { name: "v1.0" },
    });
    expect(forbidden.status()).toBe(403);

    const created = await api.post(`/api/projects/${project.id}/milestones`, {
      headers: bearer(owner),
      data: { name: "v1.0", dueDate: "2026-12-31" },
    });
    expect(created.status()).toBe(201);
    const body = await created.json();
    expect(body.name).toBe("v1.0");
    expect(body.dueDate).toBe("2026-12-31");
  });

  test("API-3502 一覧は進捗(完了/全数/見積合計)を返す", async ({ api, data }) => {
    const owner = await data.createUser("オーナー");
    const project = await data.createProject(owner);
    const milestone = await (await api.post(`/api/projects/${project.id}/milestones`, {
      headers: bearer(owner), data: { name: "v1.0" },
    })).json();
    const done = await data.createTask(project.id, owner, { status: "DONE", estimatePoints: 3 });
    const todo = await data.createTask(project.id, owner, { status: "TODO", estimatePoints: 5 });
    for (const t of [done, todo]) {
      await api.patch(`/api/projects/${project.id}/tasks/${t.id}/milestone`, {
        headers: bearer(owner), data: { milestoneId: milestone.id },
      });
    }

    const list = await (await api.get(`/api/projects/${project.id}/milestones`, { headers: bearer(owner) })).json();
    expect(list[0].progress).toEqual({ done: 1, total: 2, points: 8 });
  });

  test("API-3503 タスクの割り当て・解除、別PJマイルストーンは 400", async ({ api, data }) => {
    const owner = await data.createUser("オーナー");
    const project = await data.createProject(owner);
    const milestone = await (await api.post(`/api/projects/${project.id}/milestones`, {
      headers: bearer(owner), data: { name: "v1.0" },
    })).json();
    const task = await data.createTask(project.id, owner);

    // 割り当て
    const assign = await api.patch(`/api/projects/${project.id}/tasks/${task.id}/milestone`, {
      headers: bearer(owner), data: { milestoneId: milestone.id },
    });
    expect(assign.status()).toBe(204);
    expect((await (await api.get(`/api/tasks/${task.id}`, { headers: bearer(owner) })).json()).milestoneId).toBe(milestone.id);

    // 未割り当てへ解除
    await api.patch(`/api/projects/${project.id}/tasks/${task.id}/milestone`, {
      headers: bearer(owner), data: { milestoneId: null },
    });
    expect((await (await api.get(`/api/tasks/${task.id}`, { headers: bearer(owner) })).json()).milestoneId).toBeNull();

    // 別プロジェクトのマイルストーンは不正
    const otherProject = await data.createProject(owner);
    const otherMilestone = await (await api.post(`/api/projects/${otherProject.id}/milestones`, {
      headers: bearer(owner), data: { name: "別v1.0" },
    })).json();
    const response = await api.patch(`/api/projects/${project.id}/tasks/${task.id}/milestone`, {
      headers: bearer(owner), data: { milestoneId: otherMilestone.id },
    });
    await expectValidationError(response, "milestoneId");
  });

  test("API-3504 削除でタスクは未割り当てへ戻る", async ({ api, data }) => {
    const owner = await data.createUser("オーナー");
    const project = await data.createProject(owner);
    const milestone = await (await api.post(`/api/projects/${project.id}/milestones`, {
      headers: bearer(owner), data: { name: "v1.0" },
    })).json();
    const task = await data.createTask(project.id, owner);
    await api.patch(`/api/projects/${project.id}/tasks/${task.id}/milestone`, {
      headers: bearer(owner), data: { milestoneId: milestone.id },
    });

    const del = await api.delete(`/api/milestones/${milestone.id}`, { headers: bearer(owner) });
    expect(del.status()).toBe(204);
    expect((await (await api.get(`/api/tasks/${task.id}`, { headers: bearer(owner) })).json()).milestoneId).toBeNull();
  });

  test("API-3505 非所属ユーザーには 404(存在を開示しない)", async ({ api, data }) => {
    const owner = await data.createUser("オーナー");
    const outsider = await data.createUser("部外者");
    const project = await data.createProject(owner);

    const response = await api.get(`/api/projects/${project.id}/milestones`, { headers: bearer(outsider) });
    expect(response.status()).toBe(404);
  });
});
