import { bearer, expect, test } from "../../support/fixtures";
import { modal, signIn } from "../../support/ui";

test.describe("7.18 SCR-018 タスク依存 (M2)", () => {
  test("SCR-018-01 ブロッカーを追加すると一覧に表示され、未完了なら警告が出る", async ({ page, data }) => {
    const owner = await data.createUser("オーナー");
    const project = await data.createProject(owner);
    const taskA = await data.createTask(project.id, owner, { title: "実装タスクA" });
    await data.createTask(project.id, owner, { title: "前提タスクB" });
    await signIn(page, owner);

    await page.goto(`/tasks/${taskA.id}`);

    // 依存関係フォーム(対象タスク select を持つフォーム)を特定して操作する
    const form = page.locator("form").filter({ has: page.getByLabel("対象タスク") });
    await form.getByLabel("対象タスク").selectOption({ label: "前提タスクB" });
    await form.getByRole("button", { name: "追加" }).click();

    // ブロッカーとして表示され、未完了のため警告バナーが出る
    await expect(page.getByRole("link", { name: /前提タスクB/ })).toBeVisible();
    await expect(page.getByText(/未完了のブロッカーが 1 件あります/)).toBeVisible();
  });

  test("SCR-018-02 ブロッカーを削除すると一覧から消える", async ({ page, data }) => {
    const owner = await data.createUser("オーナー");
    const project = await data.createProject(owner);
    const taskA = await data.createTask(project.id, owner, { title: "実装タスクA" });
    await data.createTask(project.id, owner, { title: "前提タスクB" });
    await signIn(page, owner);

    await page.goto(`/tasks/${taskA.id}`);
    const form = page.locator("form").filter({ has: page.getByLabel("対象タスク") });
    await form.getByLabel("対象タスク").selectOption({ label: "前提タスクB" });
    await form.getByRole("button", { name: "追加" }).click();

    await expect(page.getByRole("link", { name: /前提タスクB/ })).toBeVisible();

    await page.getByRole("button", { name: "依存関係を削除" }).click();

    await expect(page.getByRole("link", { name: /前提タスクB/ })).toHaveCount(0);
  });

  test("SCR-018-03 未完了ブロッカーがあると完了時に確認モーダルが出て、確定すると全て完了になる", async ({
    page,
    data,
    api,
  }) => {
    const owner = await data.createUser("オーナー");
    const project = await data.createProject(owner);
    const taskA = await data.createTask(project.id, owner, { title: "本体タスクA" });
    const blockerB = await data.createTask(project.id, owner, { title: "前提タスクB" });
    // A は B に待たされる(未完了のブロッカー)
    await api.post(`/api/tasks/${taskA.id}/dependencies`, {
      headers: bearer(owner),
      data: { taskId: blockerB.id, relation: "BLOCKED_BY" },
    });
    await signIn(page, owner);
    await page.goto(`/tasks/${taskA.id}`);

    await page.getByRole("button", { name: "編集" }).click();
    await page.getByLabel("ステータス").selectOption({ label: "完了" });
    await page.getByRole("button", { name: "保存" }).click();

    // 確認モーダルにブロッカーが列挙される
    const confirm = modal(page, "未完了のブロッカーがあります");
    await expect(confirm).toBeVisible();
    await expect(confirm.getByText("前提タスクB")).toBeVisible();

    await confirm.getByRole("button", { name: "全て完了にする" }).click();

    // モーダルが閉じ、本体タスク・ブロッカーともに完了になる
    await expect(modal(page, "未完了のブロッカーがあります")).toHaveCount(0);
    await expect
      .poll(async () => (await (await api.get(`/api/tasks/${taskA.id}`, { headers: bearer(owner) })).json()).status,
        { timeout: 15000 })
      .toBe("DONE");
    const bAfter = await (await api.get(`/api/tasks/${blockerB.id}`, { headers: bearer(owner) })).json();
    expect(bAfter.status).toBe("DONE");
  });

  test("SCR-018-04 ブロッカーが無ければ確認モーダルは出ずに完了できる", async ({ page, data, api }) => {
    const owner = await data.createUser("オーナー");
    const project = await data.createProject(owner);
    const taskA = await data.createTask(project.id, owner, { title: "単独タスクA" });
    await signIn(page, owner);
    await page.goto(`/tasks/${taskA.id}`);

    await page.getByRole("button", { name: "編集" }).click();
    await page.getByLabel("ステータス").selectOption({ label: "完了" });
    await page.getByRole("button", { name: "保存" }).click();

    // モーダルは出ず、そのまま完了になる
    await expect(modal(page, "未完了のブロッカーがあります")).toHaveCount(0);
    await expect
      .poll(async () => (await (await api.get(`/api/tasks/${taskA.id}`, { headers: bearer(owner) })).json()).status,
        { timeout: 15000 })
      .toBe("DONE");
  });
});
