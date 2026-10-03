import { randomBytes } from "node:crypto";
import { defineConfig, devices } from "@playwright/test";
import { API_URL, DB_CONFIG, DB_CONNECTION_STRING, ORIGIN_VERIFY_SECRET, WEB_PORT, WEB_URL } from "./support/env";

export default defineConfig({
  testDir: "./tests",
  // テストごとに専用のユーザー・プロジェクトを作成して使うため、並列実行しても互いに干渉しない
  fullyParallel: true,
  forbidOnly: !!process.env.CI,
  retries: 0,
  timeout: 30_000,
  expect: { timeout: 10_000 },
  reporter: [["list"], ["html", { open: "never" }]],

  use: {
    baseURL: WEB_URL,
    // 日付表示(YYYY/MM/DD)の検証結果が実行環境のタイムゾーンで変わらないよう固定する
    locale: "ja-JP",
    timezoneId: "Asia/Tokyo",
    trace: "retain-on-failure",
    screenshot: "only-on-failure",
  },

  projects: [
    { name: "api", testDir: "./tests/api" },
    { name: "integration", testDir: "./tests/integration", use: { ...devices["Desktop Chrome"] } },
    { name: "screen", testDir: "./tests/screen", use: { ...devices["Desktop Chrome"] } },
    { name: "system", testDir: "./tests/system", use: { ...devices["Desktop Chrome"] } },
    { name: "security", testDir: "./tests/security", use: { ...devices["Desktop Chrome"] } },
  ],

  webServer: [
    {
      command: "node scripts/start-backend.mjs",
      // APIが起動していれば、共有シークレットなしのこの確認リクエストには403(未設定時は401)が返る。
      // Playwright は 4xx も「起動完了」とみなすため、これで待ち受けできる
      url: `${API_URL}/api/users`,
      // 開発用サーバーを誤って使わないよう、既存プロセスの再利用はしない(ポート使用中ならエラーにする)
      reuseExistingServer: false,
      timeout: 300_000,
      stdout: "pipe",
      stderr: "pipe",
      env: {
        E2E_API_URL: API_URL,
        E2E_WEB_URL: WEB_URL,
        E2E_DB_CONNECTION_STRING: DB_CONNECTION_STRING,
        E2E_DB_HOST: DB_CONFIG.host,
        E2E_DB_PORT: String(DB_CONFIG.port),
        E2E_DB_USER: DB_CONFIG.user,
        E2E_DB_PASSWORD: DB_CONFIG.password,
        E2E_DB_NAME: DB_CONFIG.database,
        // APIが検証する共有シークレット(start-backend.mjs が dotnet に引き継ぐ。設定キー OriginVerify:Secret)
        OriginVerify__Secret: ORIGIN_VERIFY_SECRET,
        // IP単位のログイン制限を短時間で確認できるよう上限を下げる(設定キー LoginProtection:MaxFailedAttemptsPerIp)
        LoginProtection__MaxFailedAttemptsPerIp: "8",
      },
    },
    {
      // 開発サーバーの .next と衝突しないよう、別ディレクトリに本番ビルドして起動する
      command: `npm run build && npm run start -- --port ${WEB_PORT}`,
      cwd: "../frontend",
      url: `${WEB_URL}/login`,
      reuseExistingServer: false,
      timeout: 300_000,
      stdout: "ignore",
      stderr: "pipe",
      env: {
        // BFFがサーバー側から呼び出すAPIのURL(ブラウザには公開されない)
        API_BASE_URL: `${API_URL}/api`,
        // セッションCookieの暗号化鍵。テスト実行ごとに生成する
        SESSION_SECRET: randomBytes(32).toString("hex"),
        // BFFがAPI呼び出しに付ける共有シークレット(APIの OriginVerify:Secret と一致させる)
        ORIGIN_VERIFY_SECRET,
        NEXT_DIST_DIR: ".next-e2e",
      },
    },
  ],
});
