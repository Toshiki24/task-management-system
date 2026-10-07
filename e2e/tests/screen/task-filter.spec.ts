import { expect, test } from "../../support/fixtures";
import { signIn } from "../../support/ui";

test.describe("7.14 SCR-014 タスク一覧の絞り込み (M2)", () => {
  test("SCR-014-01 状態チップで一覧を絞り込める", async ({ page, data }) => {
    const owner = await data.createUser("オーナー");
    const project = await data.createProject(owner);
    const todo = await data.createTask(project.id, owner, { status: "TODO", title: "未着手タスク" });
    const doing = await data.createTask(project.id, owner, { status: "IN_PROGRESS", title: "対応中タスク" });
    await signIn(page, owner);

    await page.goto(`/projects/${project.id}/tasks`);

    // 初期はリスト表示で両方見える
    await expect(page.getByRole("cell", { name: todo.title })).toBeVisible();
    await expect(page.getByRole("cell", { name: doing.title })).toBeVisible();

    // 「対応中」状態チップで絞り込む
    await page.getByRole("button", { name: "対応中", pressed: false }).click();

    await expect(page.getByRole("cell", { name: doing.title })).toBeVisible();
    await expect(page.getByRole("cell", { name: todo.title })).toHaveCount(0);
  });

  test("SCR-014-02 担当者フィルタで自分の担当だけに絞れる", async ({ page, data }) => {
    const owner = await data.createUser("オーナー");
    const other = await data.createUser("別担当");
    const project = await data.createProject(owner);
    await data.addMember(project.id, owner, other);
    const mine = await data.createTask(project.id, owner, { assigneeId: owner.id, title: "自分の担当" });
    const theirs = await data.createTask(project.id, owner, { assigneeId: other.id, title: "他人の担当" });
    await signIn(page, owner);

    await page.goto(`/projects/${project.id}/tasks`);
    await page.getByLabel("担当者").selectOption("me");

    await expect(page.getByRole("cell", { name: mine.title })).toBeVisible();
    await expect(page.getByRole("cell", { name: theirs.title })).toHaveCount(0);
  });
});
