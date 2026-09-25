import type { Page } from "@playwright/test";
import { bearer, expect, test, unique, type TestDataFactory } from "../../support/fixtures";
import { alertMessage, expectNoRequestSent, recordApiRequests, signIn, validity } from "../../support/ui";

/** 登録後の詳細画面URLからプロジェクトIDを取得し、後片付け対象に登録する */
async function expectNavigatedToCreatedProject(page: Page, data: TestDataFactory): Promise<number> {
  await expect(page).toHaveURL(/\/projects\/\d+$/);
  const id = Number(new URL(page.url()).pathname.split("/").pop());
  data.trackProject(id);
  return id;
}

test.describe("7.3 SCR-004 プロジェクト登録画面", () => {
  test("SCR-004-01 登録成功（全項目入力）", async ({ page, data, api }) => {
    const user = await data.createUser();
    await signIn(page, user);
    await page.goto("/projects/new");
    const name = unique("E2E画面登録");

    await page.getByLabel("プロジェクト名 *").fill(name);
    await page.getByLabel("説明").fill("画面から登録した説明");
    await page.getByLabel("ステータス").selectOption("COMPLETED");
    await page.getByLabel("開始日").fill("2026-10-01");
    await page.getByLabel("終了日").fill("2026-12-31");
    await page.getByRole("button", { name: "登録" }).click();

    const id = await expectNavigatedToCreatedProject(page, data);
    const saved = await (await api.get(`/api/projects/${id}`, { headers: bearer(user) })).json();
    expect(saved).toEqual({
      id,
      name,
      description: "画面から登録した説明",
      status: "COMPLETED",
      startDate: "2026-10-01",
      endDate: "2026-12-31",
    });
  });

  test("SCR-004-02 登録成功（日付未入力）", async ({ page, data }) => {
    const user = await data.createUser();
    await signIn(page, user);
    await page.goto("/projects/new");
    const name = unique("E2E名前のみ");

    await page.getByLabel("プロジェクト名 *").fill(name);
    await page.getByRole("button", { name: "登録" }).click();

    await expectNavigatedToCreatedProject(page, data);
    await expect(page.getByText(name)).toBeVisible();
  });

  test("SCR-004-03 プロジェクト名未入力", async ({ page, data }) => {
    const user = await data.createUser();
    await signIn(page, user);
    await page.goto("/projects/new");
    const createRequests = recordApiRequests(page, "POST", /^\/api\/projects$/);

    await page.getByRole("button", { name: "登録" }).click();

    expect(await validity(page.getByLabel("プロジェクト名 *"))).toMatchObject({ valueMissing: true });
    await expect(page).toHaveURL("/projects/new");
    await expectNoRequestSent(page, createRequests);
  });

  test("SCR-004-04 終了日が開始日より前", async ({ page, data }) => {
    const user = await data.createUser();
    await signIn(page, user);
    await page.goto("/projects/new");
    const createRequests = recordApiRequests(page, "POST", /^\/api\/projects$/);

    await page.getByLabel("プロジェクト名 *").fill(unique("E2E日付逆転"));
    await page.getByLabel("開始日").fill("2026-12-31");
    await page.getByLabel("終了日").fill("2026-10-01");
    await page.getByRole("button", { name: "登録" }).click();

    await expect(alertMessage(page)).toHaveText("終了日は開始日以降の日付を指定してください。");
    await expect(page).toHaveURL("/projects/new");
    await expectNoRequestSent(page, createRequests);
  });

  test("SCR-004-05 キャンセル", async ({ page, data }) => {
    const user = await data.createUser();
    await signIn(page, user);
    await page.goto("/projects/new");
    const createRequests = recordApiRequests(page, "POST", /^\/api\/projects$/);

    await page.getByLabel("プロジェクト名 *").fill(unique("E2Eキャンセル"));
    await page.getByRole("button", { name: "キャンセル" }).click();

    await expect(page).toHaveURL("/projects");
    await expectNoRequestSent(page, createRequests);
  });

  test("SCR-004-06 作成者がメンバーに自動登録される", async ({ page, data }) => {
    const user = await data.createUser();
    await signIn(page, user);
    await page.goto("/projects/new");

    await page.getByLabel("プロジェクト名 *").fill(unique("E2Eメンバー確認"));
    await page.getByRole("button", { name: "登録" }).click();

    await expectNavigatedToCreatedProject(page, data);
    await expect(page.getByRole("listitem").filter({ hasText: user.name })).toContainText(
      `${user.name} (${user.email} / OWNER)`,
    );
  });
});
