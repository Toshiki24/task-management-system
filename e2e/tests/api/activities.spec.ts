import { bearer, expect, test } from "../../support/fixtures";

type Activity = { verb: string; payload: Record<string, unknown> | null };

test.describe("5.21 アクティビティAPI (M3)", () => {
  test("API-3101 タスク作成で CREATED が記録される", async ({ api, data }) => {
    const owner = await data.createUser("オーナー");
    const project = await data.createProject(owner);
    const task = await data.createTask(project.id, owner, { title: "作成タスク" });

    const activities: Activity[] = await (await api.get(`/api/tasks/${task.id}/activities`, {
      headers: bearer(owner),
    })).json();

    const created = activities.find((a) => a.verb === "CREATED");
    expect(created).toBeTruthy();
    expect(created!.payload!.title).toBe("作成タスク");
  });

  test("API-3102 更新・移動・コメントでそれぞれ記録される", async ({ api, data }) => {
    const owner = await data.createUser("オーナー");
    const project = await data.createProject(owner);
    const task = await data.createTask(project.id, owner, { title: "元", status: "TODO" });

    // 更新(タイトル＋状態)
    await api.put(`/api/tasks/${task.id}`, {
      headers: bearer(owner),
      data: { title: "新", status: "IN_PROGRESS" },
    });
    // 移動
    await api.patch(`/api/projects/${project.id}/tasks/${task.id}/move`, {
      headers: bearer(owner),
      data: { toStatus: "DONE", beforeTaskId: null },
    });
    // コメント
    await api.post(`/api/tasks/${task.id}/comments`, {
      headers: bearer(owner),
      data: { comment: "対応しました" },
    });

    const activities: Activity[] = await (await api.get(`/api/tasks/${task.id}/activities`, {
      headers: bearer(owner),
    })).json();
    const verbs = activities.map((a) => a.verb);
    expect(verbs).toEqual(expect.arrayContaining(["CREATED", "UPDATED", "MOVED", "COMMENTED"]));

    const updated = activities.find((a) => a.verb === "UPDATED")!;
    expect(updated.payload!.fields).toEqual(expect.arrayContaining(["タイトル", "状態"]));
    const moved = activities.find((a) => a.verb === "MOVED")!;
    expect(moved.payload!.from).toBe("IN_PROGRESS");
    expect(moved.payload!.to).toBe("DONE");
  });

  test("API-3103 非所属ユーザーには 404(存在を開示しない)", async ({ api, data }) => {
    const owner = await data.createUser("オーナー");
    const outsider = await data.createUser("部外者");
    const project = await data.createProject(owner);
    const task = await data.createTask(project.id, owner);

    const response = await api.get(`/api/tasks/${task.id}/activities`, { headers: bearer(outsider) });
    expect(response.status()).toBe(404);
  });
});
