import { expect, test } from "../../support/fixtures";
import { signIn } from "../../support/ui";

test.describe("7.37 SCR-037 CSV インポート (M5)", () => {
  test("SCR-037-01 CSV を取り込むと結果が表示され、一覧に反映される", async ({ page, data }) => {
    const owner = await data.createUser("オーナー");
    const project = await data.createProject(owner);
    await signIn(page, owner);
    await page.goto(`/projects/${project.id}/tasks`);

    await page.getByRole("button", { name: "CSV インポート" }).click();

    const csv = "タイトル,状態,担当者,優先度,期限,見積,ラベル\r\n取り込みタスク,対応中,,高,,,\r\n";
    await page.getByLabel("CSV ファイル").setInputFiles({
      name: "import.csv",
      mimeType: "text/csv",
      buffer: Buffer.from("﻿" + csv, "utf-8"),
    });
    await page.getByRole("button", { name: "取り込む" }).click();

    await expect(page.getByText("1 件を取り込みました。")).toBeVisible();
    // 取り込み後、裏の一覧が再取得されて反映される
    await expect(page.getByText("取り込みタスク")).toBeVisible();
  });
});
