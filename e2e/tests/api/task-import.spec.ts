import { bearer, expect, test } from "../../support/fixtures";

const HEADER = "タイトル,状態,担当者,優先度,期限,見積,ラベル\r\n";

function postCsv(api: any, projectId: number, user: any, csv: string) {
  return api.post(`/api/projects/${projectId}/tasks/import`, {
    headers: { ...bearer(user), "content-type": "text/csv" },
    data: csv,
  });
}

test.describe("5.37 タスクCSVインポートAPI (M5)", () => {
  test("API-4701 有効行を取り込み、無効行はエラーとして返す", async ({ data, api }) => {
    const owner = await data.createUser("オーナー");
    const project = await data.createProject(owner);
    const csv = HEADER
      + "新規A,対応中,,高,2026-03-01,5,\r\n"
      + ",対応中,,,,,\r\n"            // タイトル無し → エラー
      + "新規B,ないない,,,,,\r\n";     // 状態不正 → エラー

    const res = await postCsv(api, project.id, owner, csv);
    expect(res.status()).toBe(200);
    const body = await res.json();
    expect(body.imported).toBe(1);
    expect(body.failed.length).toBe(2);
    expect(body.failed[0].row).toBe(2);

    // 実際にタスクが作成されている
    const tasks = await (await api.get(`/api/projects/${project.id}/tasks`, { headers: bearer(owner) })).json();
    expect(tasks.some((t: { title: string }) => t.title === "新規A")).toBe(true);
  });

  test("API-4702 Viewer は取り込めない(403)", async ({ data, api }) => {
    const owner = await data.createUser("オーナー");
    const viewer = await data.createUser("閲覧者");
    const workspaceId = await data.createWorkspace(owner);
    await data.addWorkspaceMember(workspaceId, viewer, "VIEWER");
    const project = await data.createProject(owner, { workspaceId });

    const res = await postCsv(api, project.id, viewer, HEADER + "x,,,,,,\r\n");
    expect(res.status()).toBe(403);
  });

  test("API-4703 非所属ユーザーには404", async ({ data, api }) => {
    const owner = await data.createUser("オーナー");
    const outsider = await data.createUser("部外者");
    const project = await data.createProject(owner);

    const res = await postCsv(api, project.id, outsider, HEADER);
    expect(res.status()).toBe(404);
  });
});
