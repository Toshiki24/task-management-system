import { expect, test } from "../../support/fixtures";
import { signIn } from "../../support/ui";

function commentsSection(page: import("@playwright/test").Page) {
  return page.locator("div").filter({ has: page.getByRole("heading", { name: "コメント" }) }).last();
}

test.describe("7.23 SCR-023 @メンション (M3)", () => {
  test("SCR-023-01 @入力で候補が出て、選ぶと挿入され、投稿後にハイライトされる", async ({ page, data }) => {
    const owner = await data.createUser("オーナー");
    const member = await data.createUser("担当");
    const project = await data.createProject(owner);
    await data.addMember(project.id, owner, member);
    const task = await data.createTask(project.id, owner);
    await signIn(page, owner);
    await page.goto(`/tasks/${task.id}`);

    const textarea = page.getByPlaceholder("コメントを入力してください");
    await textarea.click();
    await textarea.fill("@");

    const listbox = page.getByRole("listbox", { name: "メンション候補" });
    await expect(listbox).toBeVisible();
    await listbox.getByText(`@${member.name}`, { exact: true }).click();

    await expect(textarea).toHaveValue(`@${member.name} `);

    await page.getByRole("button", { name: "投稿" }).click();

    // 投稿後、コメント欄にメンションがハイライト表示される
    const highlight = commentsSection(page).locator("span.bg-blue-50");
    await expect(highlight).toContainText(member.name);
  });

  test("SCR-023-02 解決済みメンションを含むコメントがハイライト表示される", async ({ page, data }) => {
    const owner = await data.createUser("オーナー");
    const member = await data.createUser("担当");
    const project = await data.createProject(owner);
    await data.addMember(project.id, owner, member);
    const task = await data.createTask(project.id, owner);
    await data.createComment(task.id, owner, `@${member.name} お願いします`);
    await signIn(page, owner);

    await page.goto(`/tasks/${task.id}`);

    await expect(commentsSection(page).locator("span.bg-blue-50")).toContainText(member.name);
  });
});
