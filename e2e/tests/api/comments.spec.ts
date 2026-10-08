import { bearer, expect, expectValidationError, NON_EXISTENT_ID, test, unique } from "../../support/fixtures";

const TASK_NOT_FOUND = { message: "指定されたタスクが存在しません。" };

test.describe("5.6 コメントAPI", () => {
  test("API-2301 コメント一覧取得", async ({ api, data }) => {
    const owner = await data.createUser("オーナー");
    const member = await data.createUser("メンバー");
    const project = await data.createProject(owner);
    await data.addMember(project.id, owner, member);
    const task = await data.createTask(project.id, owner);
    const comment1 = await data.createComment(task.id, owner);
    const comment2 = await data.createComment(task.id, member);

    const response = await api.get(`/api/tasks/${task.id}/comments`, { headers: bearer(owner) });

    expect(response.status()).toBe(200);
    expect(await response.json()).toEqual([
      { ...comment1, userName: owner.name, edited: false, isDeleted: false },
      { ...comment2, userName: member.name, edited: false, isDeleted: false },
    ]);
  });

  test("API-2302 存在しないタスクのコメント一覧", async ({ api, data }) => {
    const user = await data.createUser();

    const response = await api.get(`/api/tasks/${NON_EXISTENT_ID}/comments`, { headers: bearer(user) });

    expect(response.status()).toBe(404);
    expect(await response.json()).toEqual(TASK_NOT_FOUND);
  });

  test("API-2401 コメント登録成功", async ({ api, data }) => {
    const user = await data.createUser();
    const project = await data.createProject(user);
    const task = await data.createTask(project.id, user);
    const comment = unique("E2Eコメント");

    const response = await api.post(`/api/tasks/${task.id}/comments`, { headers: bearer(user), data: { comment } });

    expect(response.status()).toBe(201);
    const body = await response.json();
    expect(Object.keys(body).sort()).toEqual(["comment", "createdAt", "id", "taskId", "userId"]);
    expect(body).toEqual({
      id: expect.any(Number),
      taskId: task.id,
      userId: user.id,
      comment,
      createdAt: expect.any(String),
    });
  });

  test("API-2402 投稿者がJWTのユーザーになる", async ({ api, data, db }) => {
    const author = await data.createUser("投稿者");
    const other = await data.createUser("別ユーザー");
    const project = await data.createProject(author);
    const task = await data.createTask(project.id, author);

    const response = await api.post(`/api/tasks/${task.id}/comments`, {
      headers: bearer(author),
      data: { comment: unique("E2Eコメント"), userId: other.id },
    });

    expect(response.status()).toBe(201);
    const body = await response.json();
    expect(body.userId).toBe(author.id);
    const { rows } = await db.query<{ user_id: string }>("SELECT user_id FROM task_comments WHERE id = $1", [body.id]);
    expect(Number(rows[0].user_id)).toBe(author.id);
  });

  test("API-2403 comment未指定", async ({ api, data }) => {
    const user = await data.createUser();
    const project = await data.createProject(user);
    const task = await data.createTask(project.id, user);

    const response = await api.post(`/api/tasks/${task.id}/comments`, { headers: bearer(user), data: {} });

    await expectValidationError(response, "comment", "コメントは必須です。");
  });

  test("API-2404 comment最大文字数超過", async ({ api, data }) => {
    const user = await data.createUser();
    const project = await data.createProject(user);
    const task = await data.createTask(project.id, user);

    const response = await api.post(`/api/tasks/${task.id}/comments`, {
      headers: bearer(user),
      data: { comment: "あ".repeat(1001) },
    });

    await expectValidationError(response, "comment", "コメントは1000文字以内で入力してください。");
  });

  test("API-2405 存在しないタスクへの投稿", async ({ api, data }) => {
    const user = await data.createUser();

    const response = await api.post(`/api/tasks/${NON_EXISTENT_ID}/comments`, {
      headers: bearer(user),
      data: { comment: unique("E2Eコメント") },
    });

    expect(response.status()).toBe(404);
    expect(await response.json()).toEqual(TASK_NOT_FOUND);
  });

  test("API-2406 投稿者はコメントを編集でき、edited=true になる (M3)", async ({ api, data }) => {
    const user = await data.createUser();
    const project = await data.createProject(user);
    const task = await data.createTask(project.id, user);
    const created = await data.createComment(task.id, user);

    const response = await api.patch(`/api/tasks/${task.id}/comments/${created.id}`, {
      headers: bearer(user),
      data: { comment: "編集しました" },
    });

    expect(response.status()).toBe(200);
    const body = await response.json();
    expect(body.comment).toBe("編集しました");
    expect(body.edited).toBe(true);
  });

  test("API-2407 他人のコメントは編集できない (403, M3)", async ({ api, data }) => {
    const owner = await data.createUser("オーナー");
    const member = await data.createUser("メンバー");
    const project = await data.createProject(owner);
    await data.addMember(project.id, owner, member);
    const task = await data.createTask(project.id, owner);
    const created = await data.createComment(task.id, owner);

    const response = await api.patch(`/api/tasks/${task.id}/comments/${created.id}`, {
      headers: bearer(member),
      data: { comment: "横取り" },
    });

    expect(response.status()).toBe(403);
  });

  test("API-2408 投稿者は削除でき、一覧では本文が伏せられる (M3)", async ({ api, data }) => {
    const user = await data.createUser();
    const project = await data.createProject(user);
    const task = await data.createTask(project.id, user);
    const created = await data.createComment(task.id, user);

    const del = await api.delete(`/api/tasks/${task.id}/comments/${created.id}`, { headers: bearer(user) });
    expect(del.status()).toBe(204);

    const list = await (await api.get(`/api/tasks/${task.id}/comments`, { headers: bearer(user) })).json();
    expect(list).toHaveLength(1);
    expect(list[0].isDeleted).toBe(true);
    expect(list[0].comment).toBeNull();
  });

  test("API-2409 プロジェクト管理者は他人のコメントを削除できる (M3)", async ({ api, data }) => {
    const owner = await data.createUser("オーナー");
    const member = await data.createUser("メンバー");
    const project = await data.createProject(owner);
    await data.addMember(project.id, owner, member);
    const task = await data.createTask(project.id, owner);
    const created = await data.createComment(task.id, member);

    // owner はプロジェクト OWNER / WS Admin なので他人のコメントを削除できる
    const del = await api.delete(`/api/tasks/${task.id}/comments/${created.id}`, { headers: bearer(owner) });
    expect(del.status()).toBe(204);
  });
});
