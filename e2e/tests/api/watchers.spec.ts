import { bearer, expect, test } from "../../support/fixtures";

test.describe("5.22 ウォッチャーAPI (M3)", () => {
  test("API-3201 フォロー/解除ができ、watching と一覧に反映される", async ({ api, data }) => {
    const owner = await data.createUser("オーナー");
    const project = await data.createProject(owner);
    const task = await data.createTask(project.id, owner);

    const before = await (await api.get(`/api/tasks/${task.id}/watchers`, { headers: bearer(owner) })).json();
    expect(before.watching).toBe(false);

    const watched = await (await api.post(`/api/tasks/${task.id}/watch`, { headers: bearer(owner) })).json();
    expect(watched.watching).toBe(true);
    expect(watched.watchers.map((w: { userId: number }) => w.userId)).toContain(owner.id);

    const unwatched = await (await api.delete(`/api/tasks/${task.id}/watch`, { headers: bearer(owner) })).json();
    expect(unwatched.watching).toBe(false);
  });

  test("API-3202 担当者はタスク作成で自動ウォッチされる", async ({ api, data }) => {
    const owner = await data.createUser("オーナー");
    const member = await data.createUser("担当");
    const project = await data.createProject(owner);
    await data.addMember(project.id, owner, member);
    const task = await data.createTask(project.id, owner, { assigneeId: member.id });

    const watchers = await (await api.get(`/api/tasks/${task.id}/watchers`, { headers: bearer(owner) })).json();
    expect(watchers.watchers.map((w: { userId: number }) => w.userId)).toContain(member.id);
  });

  test("API-3203 コメント投稿者と被メンション者が自動ウォッチされる", async ({ api, data }) => {
    const owner = await data.createUser("オーナー");
    const member = await data.createUser("担当");
    const project = await data.createProject(owner);
    await data.addMember(project.id, owner, member);
    const task = await data.createTask(project.id, owner);

    await api.post(`/api/tasks/${task.id}/comments`, {
      headers: bearer(owner),
      data: { comment: `@${member.name} お願いします` },
    });

    const watchers = await (await api.get(`/api/tasks/${task.id}/watchers`, { headers: bearer(owner) })).json();
    const ids = watchers.watchers.map((w: { userId: number }) => w.userId);
    expect(ids).toContain(owner.id);
    expect(ids).toContain(member.id);
  });

  test("API-3204 非所属ユーザーには 404(存在を開示しない)", async ({ api, data }) => {
    const owner = await data.createUser("オーナー");
    const outsider = await data.createUser("部外者");
    const project = await data.createProject(owner);
    const task = await data.createTask(project.id, owner);

    const response = await api.get(`/api/tasks/${task.id}/watchers`, { headers: bearer(outsider) });
    expect(response.status()).toBe(404);
  });
});
