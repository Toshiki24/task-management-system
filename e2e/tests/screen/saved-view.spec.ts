import { expect, test, unique } from "../../support/fixtures";
import { signIn } from "../../support/ui";

test.describe("7.15 SCR-015 保存ビュー (M2)", () => {
  test("SCR-015-01 現在の絞り込みをビューとして保存し、適用できる", async ({ page, data }) => {
    const owner = await data.createUser("オーナー");
    const project = await data.createProject(owner);
    const todo = await data.createTask(project.id, owner, { status: "TODO", title: "未着手タスク" });
    const doing = await data.createTask(project.id, owner, { status: "IN_PROGRESS", title: "対応中タスク" });
    await signIn(page, owner);

    await page.goto(`/projects/${project.id}/tasks`);

    // 「対応中」で絞り込む
    await page.getByRole("button", { name: "対応中", pressed: false }).click();
    await expect(page.getByRole("cell", { name: todo.title })).toHaveCount(0);

    // ビューとして保存する
    const viewName = unique("view");
    await page.getByRole("button", { name: "現在の条件を保存" }).click();
    await page.getByLabel("ビュー名").fill(viewName);
    await page.getByRole("button", { name: "保存", exact: true }).click();
    // 保存完了(モーダルが閉じる=POST成功)を待ってから遷移する
    await expect(page.getByLabel("ビュー名")).toHaveCount(0);

    // ページを開き直すと絞り込みは初期状態(両方表示)
    await page.goto(`/projects/${project.id}/tasks`);
    await expect(page.getByRole("cell", { name: todo.title })).toBeVisible();
    await expect(page.getByRole("cell", { name: doing.title })).toBeVisible();

    // 保存したビューを選ぶと、絞り込みが再適用される
    await page.getByLabel("保存ビュー").selectOption({ label: `${viewName}` });
    await expect(page.getByRole("cell", { name: doing.title })).toBeVisible();
    await expect(page.getByRole("cell", { name: todo.title })).toHaveCount(0);
  });
});
