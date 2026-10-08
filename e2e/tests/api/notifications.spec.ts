import { bearer, expect, test } from "../../support/fixtures";

test.describe("5.23 通知API (M3)", () => {
  test("API-3301 担当にされると ASSIGNED 通知が届き、実行者には届かない", async ({ api, data }) => {
    const owner = await data.createUser("オーナー");
    const member = await data.createUser("担当");
    const project = await data.createProject(owner);
    await data.addMember(project.id, owner, member);
    await data.createTask(project.id, owner, { assigneeId: member.id });

    const memberList = await (await api.get(`/api/me/notifications`, { headers: bearer(member) })).json();
    expect(memberList.items.some((n: { type: string }) => n.type === "ASSIGNED")).toBe(true);

    const count = await (await api.get(`/api/me/notifications/unread-count`, { headers: bearer(member) })).json();
    expect(count.count).toBeGreaterThanOrEqual(1);

    // 実行者(owner)には通知されない
    const ownerCount = await (await api.get(`/api/me/notifications/unread-count`, { headers: bearer(owner) })).json();
    expect(ownerCount.count).toBe(0);
  });

  test("API-3302 既読化・すべて既読で未読数が減る", async ({ api, data }) => {
    const owner = await data.createUser("オーナー");
    const member = await data.createUser("担当");
    const project = await data.createProject(owner);
    await data.addMember(project.id, owner, member);
    const task = await data.createTask(project.id, owner, { assigneeId: member.id });
    // 状態変更でもう1件(member はウォッチャー)
    await api.patch(`/api/projects/${project.id}/tasks/${task.id}/move`, {
      headers: bearer(owner),
      data: { toStatus: "DONE", beforeTaskId: null },
    });

    const list = await (await api.get(`/api/me/notifications`, { headers: bearer(member) })).json();
    expect(list.items.length).toBeGreaterThanOrEqual(2);

    const read = await api.post(`/api/me/notifications/${list.items[0].id}/read`, { headers: bearer(member) });
    expect(read.status()).toBe(204);

    await api.post(`/api/me/notifications/read-all`, { headers: bearer(member) });
    const count = await (await api.get(`/api/me/notifications/unread-count`, { headers: bearer(member) })).json();
    expect(count.count).toBe(0);
  });

  test("API-3303 他人の通知は既読化できない (404)", async ({ api, data }) => {
    const owner = await data.createUser("オーナー");
    const member = await data.createUser("担当");
    const project = await data.createProject(owner);
    await data.addMember(project.id, owner, member);
    await data.createTask(project.id, owner, { assigneeId: member.id });
    const memberNotif = (await (await api.get(`/api/me/notifications`, { headers: bearer(member) })).json()).items[0];

    const response = await api.post(`/api/me/notifications/${memberNotif.id}/read`, { headers: bearer(owner) });
    expect(response.status()).toBe(404);
  });
});
