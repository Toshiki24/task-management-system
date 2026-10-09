import { bearer, expect, test } from "../../support/fixtures";

test.describe("5.36 タスクCSVエクスポートAPI (M5)", () => {
  test("API-4601 ヘッダ付き CSV を返す(text/csv・タスク行を含む)", async ({ data, api }) => {
    const owner = await data.createUser("オーナー");
    const project = await data.createProject(owner);
    await data.createTask(project.id, owner, { title: "ログイン不具合", status: "DONE", priority: "HIGH" });

    const res = await api.get(`/api/projects/${project.id}/tasks/export.csv`, { headers: bearer(owner) });

    expect(res.status()).toBe(200);
    expect(res.headers()["content-type"]).toContain("text/csv");
    const text = await res.text();
    expect(text).toContain("タイトル,状態,担当者,優先度,期限,見積,ラベル");
    expect(text).toContain("ログイン不具合");
    expect(text).toContain("完了");
    expect(text).toContain("高");
  });

  test("API-4602 CSV インジェクションになりうる値は無害化される", async ({ data, api }) => {
    const owner = await data.createUser("オーナー");
    const project = await data.createProject(owner);
    await data.createTask(project.id, owner, { title: "=HYPERLINK(1)" });

    const res = await api.get(`/api/projects/${project.id}/tasks/export.csv`, { headers: bearer(owner) });
    const text = await res.text();
    // 先頭にアポストロフィが付く(数式として実行されない)
    expect(text).toContain("'=HYPERLINK(1)");
  });

  test("API-4603 非所属ユーザーには404", async ({ data, api }) => {
    const owner = await data.createUser("オーナー");
    const outsider = await data.createUser("部外者");
    const project = await data.createProject(owner);

    const res = await api.get(`/api/projects/${project.id}/tasks/export.csv`, { headers: bearer(outsider) });
    expect(res.status()).toBe(404);
  });
});
