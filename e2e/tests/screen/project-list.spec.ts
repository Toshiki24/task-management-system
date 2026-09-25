import { API_URL } from "../../support/env";
import { expect, test } from "../../support/fixtures";
import { alertMessage, signIn, tableRow } from "../../support/ui";

const PROJECTS_API = `${API_URL}/api/projects`;

test.describe("7.2 SCR-003 プロジェクト一覧画面", () => {
  test("SCR-003-01 一覧表示", async ({ page, data }) => {
    const user = await data.createUser();
    const project = await data.createProject(user, {
      description: "一覧表示確認用の説明",
      status: "ACTIVE",
      startDate: "2026-10-01",
      endDate: "2026-12-31",
    });
    await signIn(page, user);

    await page.goto("/projects");

    await expect(page.getByRole("columnheader")).toHaveText(["プロジェクト名", "説明", "ステータス", "開始日", "終了日"]);
    await expect(tableRow(page, project.name).getByRole("cell")).toHaveText([
      project.name,
      "一覧表示確認用の説明",
      "ACTIVE",
      "2026/10/01",
      "2026/12/31",
    ]);
  });

  test("SCR-003-02 プロジェクトが0件の場合", async ({ page, data }) => {
    // プロジェクト一覧APIは全プロジェクトを返すため、実データを0件にすると並列実行中の他テストに影響する。
    // そのため一覧APIの応答だけを0件に差し替えて、0件時の表示を確認する。
    await page.route(PROJECTS_API, (route) =>
      route.request().method() === "GET" ? route.fulfill({ json: [] }) : route.continue(),
    );
    const user = await data.createUser();
    await signIn(page, user);

    await page.goto("/projects");

    await expect(page.getByText("プロジェクトがありません。")).toBeVisible();
    await expect(page.getByRole("link", { name: "プロジェクトを作成" })).toHaveAttribute("href", "/projects/new");
  });

  test("SCR-003-03 新規作成画面への遷移", async ({ page, data }) => {
    const user = await data.createUser();
    await signIn(page, user);
    await page.goto("/projects");

    await page.getByRole("link", { name: "＋ 新規作成" }).click();

    await expect(page).toHaveURL("/projects/new");
    await expect(page.getByRole("heading", { name: "プロジェクト登録" })).toBeVisible();
  });

  test("SCR-003-04 プロジェクト詳細への遷移", async ({ page, data }) => {
    const user = await data.createUser();
    const project = await data.createProject(user);
    await signIn(page, user);
    await page.goto("/projects");

    await tableRow(page, project.name).click();

    await expect(page).toHaveURL(`/projects/${project.id}`);
    await expect(page.getByRole("heading", { name: "プロジェクト詳細" })).toBeVisible();
  });

  test("SCR-003-05 API取得失敗時の表示", async ({ page, data }) => {
    // バックエンドを停止すると他のテストに影響するため、この画面の通信だけを遮断する
    await page.route(PROJECTS_API, (route) => route.abort("connectionrefused"));
    const user = await data.createUser();
    await signIn(page, user);

    await page.goto("/projects");

    await expect(alertMessage(page)).toHaveText("プロジェクト情報の取得に失敗しました。");
  });
});
