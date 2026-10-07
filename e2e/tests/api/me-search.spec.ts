import { bearer, expect, test, unique } from "../../support/fixtures";

test.describe("5.20 横断タスク検索API (コマンドパレット, M2)", () => {
  test("API-3001 キーワードで閲覧可能なタスクを横断検索できる", async ({ api, data }) => {
    const owner = await data.createUser("オーナー");
    const projectA = await data.createProject(owner);
    const projectB = await data.createProject(owner);
    const title = unique("リリース準備");
    const hit = await data.createTask(projectA.id, owner, { title });
    await data.createTask(projectB.id, owner, { title: unique("無関係") });

    const response = await api.get(`/api/me/search/tasks?keyword=${encodeURIComponent("リリース準備")}`, {
      headers: bearer(owner),
    });
    expect(response.status()).toBe(200);
    const results = await response.json();
    expect(results.map((r: { id: number }) => r.id)).toContain(hit.id);
    const row = results.find((r: { id: number }) => r.id === hit.id);
    expect(row.projectId).toBe(projectA.id);
    expect(row.projectName).toBe(projectA.name);
  });

  test("API-3002 所属しないワークスペースのタスクは出ない", async ({ api, data }) => {
    const me = await data.createUser("わたし");
    const other = await data.createUser("別人");
    const keyword = unique("共通KW");
    const myProject = await data.createProject(me);
    await data.createTask(myProject.id, me, { title: keyword });
    const otherProject = await data.createProject(other);
    const foreign = await data.createTask(otherProject.id, other, { title: keyword });

    const results = await (await api.get(`/api/me/search/tasks?keyword=${encodeURIComponent(keyword)}`, {
      headers: bearer(me),
    })).json();

    expect(results.map((r: { id: number }) => r.id)).not.toContain(foreign.id);
  });

  test("API-3003 limit で件数を絞れる", async ({ api, data }) => {
    const owner = await data.createUser("オーナー");
    const project = await data.createProject(owner);
    for (let i = 0; i < 4; i++) {
      await data.createTask(project.id, owner, { title: unique("絞り込み対象") });
    }

    const results = await (await api.get(`/api/me/search/tasks?keyword=${encodeURIComponent("絞り込み対象")}&limit=2`, {
      headers: bearer(owner),
    })).json();

    expect(results).toHaveLength(2);
  });
});
