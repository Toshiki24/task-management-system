import type { Page } from "@playwright/test";
import { bearer, expect, test, unique } from "../../support/fixtures";
import { detailField as field, modal, signIn, tableRow } from "../../support/ui";

function memberItems(page: Page) {
  return page.getByRole("listitem");
}

test.describe("7.4 SCR-005 プロジェクト詳細画面", () => {
  test("SCR-005-01 詳細表示", async ({ page, data }) => {
    const user = await data.createUser();
    const project = await data.createProject(user, {
      description: "詳細表示確認用の説明",
      status: "COMPLETED",
      startDate: "2026-10-01",
      endDate: "2026-12-31",
    });
    await signIn(page, user);

    await page.goto(`/projects/${project.id}`);

    await expect(field(page, "プロジェクト名")).toHaveText(project.name);
    await expect(field(page, "ステータス")).toHaveText("COMPLETED");
    await expect(field(page, "説明")).toHaveText("詳細表示確認用の説明");
    await expect(field(page, "開始日")).toHaveText("2026/10/01");
    await expect(field(page, "終了日")).toHaveText("2026/12/31");
  });

  test("SCR-005-02 編集モードへの切り替え", async ({ page, data }) => {
    const user = await data.createUser();
    const project = await data.createProject(user, { status: "ARCHIVED" });
    await signIn(page, user);
    await page.goto(`/projects/${project.id}`);

    await page.getByRole("button", { name: "編集" }).click();

    await expect(page.getByLabel("プロジェクト名 *")).toHaveValue(project.name);
    await expect(page.getByLabel("説明")).toHaveValue(project.description ?? "");
    await expect(page.getByLabel("ステータス")).toHaveValue("ARCHIVED");
    await expect(page.getByLabel("開始日")).toHaveValue(project.startDate ?? "");
    await expect(page.getByLabel("終了日")).toHaveValue(project.endDate ?? "");
  });

  test("SCR-005-03 編集の保存", async ({ page, data }) => {
    const user = await data.createUser();
    const project = await data.createProject(user);
    await signIn(page, user);
    await page.goto(`/projects/${project.id}`);
    const newName = unique("E2E編集後");

    await page.getByRole("button", { name: "編集" }).click();
    await page.getByLabel("プロジェクト名 *").fill(newName);
    await page.getByLabel("ステータス").selectOption("COMPLETED");
    await page.getByRole("button", { name: "保存" }).click();

    await expect(page.getByRole("button", { name: "編集" })).toBeVisible();
    await expect(field(page, "プロジェクト名")).toHaveText(newName);
    await expect(field(page, "ステータス")).toHaveText("COMPLETED");
  });

  test("SCR-005-04 編集のキャンセル", async ({ page, data, api }) => {
    const user = await data.createUser();
    const project = await data.createProject(user);
    await signIn(page, user);
    await page.goto(`/projects/${project.id}`);

    await page.getByRole("button", { name: "編集" }).click();
    await page.getByLabel("プロジェクト名 *").fill(unique("E2E破棄される名前"));
    await page.getByRole("button", { name: "キャンセル" }).click();

    await expect(page.getByRole("button", { name: "編集" })).toBeVisible();
    await expect(field(page, "プロジェクト名")).toHaveText(project.name);
    const saved = await (await api.get(`/api/projects/${project.id}`, { headers: bearer(user) })).json();
    expect(saved.name).toBe(project.name);
  });

  test("SCR-005-05 削除確認モーダル表示", async ({ page, data }) => {
    const user = await data.createUser();
    const project = await data.createProject(user);
    await signIn(page, user);
    await page.goto(`/projects/${project.id}`);

    await page.getByRole("button", { name: "削除", exact: true }).first().click();

    await expect(modal(page, "プロジェクト削除")).toContainText("このプロジェクトを削除しますか？");
  });

  test("SCR-005-06 削除のキャンセル", async ({ page, data, api }) => {
    const user = await data.createUser();
    const project = await data.createProject(user);
    await signIn(page, user);
    await page.goto(`/projects/${project.id}`);

    await page.getByRole("button", { name: "削除", exact: true }).first().click();
    await modal(page, "プロジェクト削除").getByRole("button", { name: "キャンセル" }).click();

    await expect(modal(page, "プロジェクト削除")).toHaveCount(0);
    await expect(page).toHaveURL(`/projects/${project.id}`);
    expect((await api.get(`/api/projects/${project.id}`, { headers: bearer(user) })).status()).toBe(200);
  });

  test("SCR-005-07 削除の実行", async ({ page, data, api }) => {
    const user = await data.createUser();
    const project = await data.createProject(user);
    await signIn(page, user);
    await page.goto(`/projects/${project.id}`);

    await page.getByRole("button", { name: "削除", exact: true }).first().click();
    await modal(page, "プロジェクト削除").getByRole("button", { name: "削除" }).click();

    await expect(page).toHaveURL("/projects");
    await expect(page.getByRole("heading", { name: "プロジェクト一覧" })).toBeVisible();
    await expect(tableRow(page, project.name)).toHaveCount(0);
    expect((await api.get(`/api/projects/${project.id}`, { headers: bearer(user) })).status()).toBe(404);
  });

  test("SCR-005-08 メンバー一覧表示", async ({ page, data }) => {
    const owner = await data.createUser("オーナー");
    const member = await data.createUser("メンバー");
    const project = await data.createProject(owner);
    await data.addMember(project.id, owner, member);
    await signIn(page, owner);

    await page.goto(`/projects/${project.id}`);

    await expect(memberItems(page)).toHaveCount(2);
    await expect(memberItems(page).nth(0)).toContainText(`${owner.name} (${owner.email} / OWNER)`);
    await expect(memberItems(page).nth(1)).toContainText(`${member.name} (${member.email} / MEMBER)`);
  });

  test("SCR-005-09 メンバー追加", async ({ page, data }) => {
    const owner = await data.createUser("オーナー");
    const newMember = await data.createUser("追加メンバー");
    const project = await data.createProject(owner);
    await signIn(page, owner);
    await page.goto(`/projects/${project.id}`);

    await page.getByRole("button", { name: "メンバー追加" }).click();
    await page.getByLabel("ユーザー").selectOption({ label: `${newMember.name} (${newMember.email})` });
    await page.getByLabel("ロール").selectOption("MEMBER");
    await page.getByRole("button", { name: "追加", exact: true }).click();

    await expect(memberItems(page).filter({ hasText: newMember.name })).toContainText(
      `${newMember.name} (${newMember.email} / MEMBER)`,
    );
  });

  test("SCR-005-10 追加候補から既存メンバーが除外される", async ({ page, data }) => {
    const owner = await data.createUser("オーナー");
    const member = await data.createUser("既存メンバー");
    const outsider = await data.createUser("未所属");
    const project = await data.createProject(owner);
    await data.addMember(project.id, owner, member);
    await signIn(page, owner);
    await page.goto(`/projects/${project.id}`);

    await page.getByRole("button", { name: "メンバー追加" }).click();

    const options = page.getByLabel("ユーザー").locator("option");
    await expect(options.filter({ hasText: outsider.email })).toHaveCount(1);
    await expect(options.filter({ hasText: owner.email })).toHaveCount(0);
    await expect(options.filter({ hasText: member.email })).toHaveCount(0);
  });

  test("SCR-005-11 メンバー削除確認モーダル", async ({ page, data }) => {
    const owner = await data.createUser("オーナー");
    const member = await data.createUser("メンバー");
    const project = await data.createProject(owner);
    await data.addMember(project.id, owner, member);
    await signIn(page, owner);
    await page.goto(`/projects/${project.id}`);

    await memberItems(page).filter({ hasText: member.name }).getByRole("button", { name: "削除" }).click();

    await expect(modal(page, "メンバー削除")).toContainText(`${member.name} をこのプロジェクトから削除しますか？`);
  });

  test("SCR-005-12 メンバー削除の実行", async ({ page, data, api }) => {
    const owner = await data.createUser("オーナー");
    const member = await data.createUser("メンバー");
    const project = await data.createProject(owner);
    await data.addMember(project.id, owner, member);
    await signIn(page, owner);
    await page.goto(`/projects/${project.id}`);

    await memberItems(page).filter({ hasText: member.name }).getByRole("button", { name: "削除" }).click();
    await modal(page, "メンバー削除").getByRole("button", { name: "削除" }).click();

    await expect(memberItems(page).filter({ hasText: member.name })).toHaveCount(0);
    await expect(memberItems(page).filter({ hasText: owner.name })).toHaveCount(1);
    const members = await (await api.get(`/api/projects/${project.id}/members`, { headers: bearer(owner) })).json();
    expect(members.map((m: { userId: number }) => m.userId)).toEqual([owner.id]);
  });

  test("SCR-005-13 タスク一覧への遷移", async ({ page, data }) => {
    const user = await data.createUser();
    const project = await data.createProject(user);
    await signIn(page, user);
    await page.goto(`/projects/${project.id}`);

    await page.getByRole("link", { name: "タスク一覧を見る" }).click();

    await expect(page).toHaveURL(`/projects/${project.id}/tasks`);
    await expect(page.getByRole("heading", { name: "タスク一覧" })).toBeVisible();
  });
});
