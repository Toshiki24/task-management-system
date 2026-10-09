import { bearer, expect, test } from "../../support/fixtures";

test.describe("5.34 指標API (M5)", () => {
  test("API-4401 進捗・完了率・状態別件数・期限超過・担当別負荷を集計する", async ({ data, api }) => {
    const owner = await data.createUser("オーナー");
    const project = await data.createProject(owner);
    await data.createTask(project.id, owner, { status: "TODO", assigneeId: owner.id, estimatePoints: 3, dueDate: "2020-01-01" });
    await data.createTask(project.id, owner, { status: "IN_PROGRESS", assigneeId: owner.id, estimatePoints: 2 });
    await data.createTask(project.id, owner, { status: "DONE" });
    await data.createTask(project.id, owner, { status: "DONE" });

    const m = await (await api.get(`/api/projects/${project.id}/metrics`, { headers: bearer(owner) })).json();

    expect(m.total).toBe(4);
    expect(m.doneCount).toBe(2);
    expect(m.completionRate).toBe(0.5);
    expect(m.overdueCount).toBe(1); // 期限超過は TODO(2020-01-01) の 1 件
    // 既定ワークフロー TODO/IN_PROGRESS/DONE の順
    expect(m.statusCounts.map((s: { count: number }) => s.count)).toEqual([1, 1, 2]);
    const ownerLoad = m.assigneeLoads.find((a: { assigneeId: number }) => a.assigneeId === owner.id);
    expect(ownerLoad.openCount).toBe(2);
    expect(ownerLoad.estimatePoints).toBe(5);
  });

  test("API-4402 非所属ユーザーには404(存在を開示しない)", async ({ data, api }) => {
    const owner = await data.createUser("オーナー");
    const outsider = await data.createUser("部外者");
    const project = await data.createProject(owner);

    const res = await api.get(`/api/projects/${project.id}/metrics`, { headers: bearer(outsider) });
    expect(res.status()).toBe(404);
  });
});
