import { bearer, expect, test } from "../../support/fixtures";

test.describe("5.16 My Tasks API (M2)", () => {
  test("API-2601 所属する全WSの自分の担当のみを横断で返す", async ({ api, data }) => {
    const me = await data.createUser("わたし");
    const other = await data.createUser("ほか");
    const wsA = await data.createWorkspace(me, "ADMIN");
    const projA = await data.createProject(me, { workspaceId: wsA });
    await data.addMember(projA.id, me, other);
    const mineA = await data.createTask(projA.id, me, { assigneeId: me.id });
    await data.createTask(projA.id, me, { assigneeId: other.id }); // 他人担当→返らない

    const wsB = await data.createWorkspace(me, "ADMIN");
    const projB = await data.createProject(me, { workspaceId: wsB });
    const mineB = await data.createTask(projB.id, me, { assigneeId: me.id });

    const response = await api.get(`/api/me/tasks`, { headers: bearer(me) });
    expect(response.status()).toBe(200);
    const body = await response.json();

    expect(body.total).toBe(2);
    expect(body.page).toBe(1);
    expect(body.items.map((t: { id: number }) => t.id).sort()).toEqual([mineA.id, mineB.id].sort());
    const a = body.items.find((t: { id: number }) => t.id === mineA.id);
    expect(a.projectName).toBe(projA.name);
    expect(a.workspaceId).toBe(wsA);
  });

  test("API-2602 非所属WSのタスクは自分担当でも返らない", async ({ api, data }) => {
    const me = await data.createUser("わたし");
    const owner = await data.createUser("オーナー");
    // me が所属しないWSのプロジェクトで、me を担当にはできない(メンバー外)ので、
    // ここでは me が所属するタスクが無いことを確認する
    const ws = await data.createWorkspace(owner, "ADMIN");
    const project = await data.createProject(owner, { workspaceId: ws });
    await data.createTask(project.id, owner, { assigneeId: owner.id });

    const response = await api.get(`/api/me/tasks`, { headers: bearer(me) });
    const body = await response.json();
    expect(body.total).toBe(0);
    expect(body.items).toEqual([]);
  });

  test("API-2603 ページングする", async ({ api, data }) => {
    const me = await data.createUser("わたし");
    const ws = await data.createWorkspace(me, "ADMIN");
    const project = await data.createProject(me, { workspaceId: ws });
    for (let i = 0; i < 3; i++) {
      await data.createTask(project.id, me, { assigneeId: me.id });
    }

    const page1 = await (await api.get(`/api/me/tasks?page=1&pageSize=2`, { headers: bearer(me) })).json();
    expect(page1.total).toBe(3);
    expect(page1.items).toHaveLength(2);

    const page2 = await (await api.get(`/api/me/tasks?page=2&pageSize=2`, { headers: bearer(me) })).json();
    expect(page2.items).toHaveLength(1);
  });

  test("API-2604 status で絞り込める", async ({ api, data }) => {
    const me = await data.createUser("わたし");
    const ws = await data.createWorkspace(me, "ADMIN");
    const project = await data.createProject(me, { workspaceId: ws });
    await data.createTask(project.id, me, { assigneeId: me.id, status: "TODO" });
    await data.createTask(project.id, me, { assigneeId: me.id, status: "IN_PROGRESS" });

    const response = await api.get(`/api/me/tasks?status=IN_PROGRESS`, { headers: bearer(me) });
    const body = await response.json();
    expect(body.total).toBe(1);
    expect(body.items[0].status).toBe("IN_PROGRESS");
  });
});
