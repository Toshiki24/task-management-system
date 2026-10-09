import { bearer, expect, test } from "../../support/fixtures";

test.describe("5.35 開発指標API (M5)", () => {
  test("API-4501 状態遷移の履歴からサイクル/リード・スループットを算出する", async ({ data, api }) => {
    const owner = await data.createUser("オーナー");
    const project = await data.createProject(owner);
    const task = await data.createTask(project.id, owner, { status: "TODO" });

    // 着手 → 完了 と状態遷移すると task_status_histories に記録される
    expect((await api.patch(`/api/projects/${project.id}/tasks/${task.id}/move`, {
      headers: bearer(owner), data: { toStatus: "IN_PROGRESS", beforeTaskId: null },
    })).status()).toBe(200);
    expect((await api.patch(`/api/projects/${project.id}/tasks/${task.id}/move`, {
      headers: bearer(owner), data: { toStatus: "DONE", beforeTaskId: null },
    })).status()).toBe(200);

    const m = await (await api.get(`/api/projects/${project.id}/metrics/dev?days=30`, { headers: bearer(owner) })).json();

    expect(m.days).toBe(30);
    expect(m.completedInPeriod).toBe(1);
    // 同一リクエスト内の連続遷移なのでサイクルタイムは 0 以上(算出されていること=null でない)
    expect(m.avgCycleTimeHours).not.toBeNull();
    expect(m.avgLeadTimeHours).not.toBeNull();
    expect(m.throughput.length).toBe(30);
    expect(m.throughput.reduce((s: number, p: { count: number }) => s + p.count, 0)).toBe(1);
  });

  test("API-4502 days は 1〜90 にクランプされる", async ({ data, api }) => {
    const owner = await data.createUser("オーナー");
    const project = await data.createProject(owner);

    const over = await (await api.get(`/api/projects/${project.id}/metrics/dev?days=999`, { headers: bearer(owner) })).json();
    expect(over.days).toBe(90);
    const under = await (await api.get(`/api/projects/${project.id}/metrics/dev?days=0`, { headers: bearer(owner) })).json();
    expect(under.days).toBe(1);
  });

  test("API-4503 非所属ユーザーには404", async ({ data, api }) => {
    const owner = await data.createUser("オーナー");
    const outsider = await data.createUser("部外者");
    const project = await data.createProject(owner);

    const res = await api.get(`/api/projects/${project.id}/metrics/dev`, { headers: bearer(outsider) });
    expect(res.status()).toBe(404);
  });
});
