// E2Eテスト用のバックエンドを起動する(playwright.config.ts の webServer から呼ばれる)。
//
// 1. テスト専用DBを削除・再作成する(開発用DBには触れない)
// 2. EF Core Migrationを適用する
// 3. テスト専用のポート・DB・CORS設定でAPIを起動する
//
// 開発中のAPI(Debugビルド)が起動していてもDLLのロックで衝突しないよう、Releaseビルドで起動する。
import { spawn, spawnSync } from "node:child_process";
import path from "node:path";
import { fileURLToPath } from "node:url";
import pg from "pg";

const { E2E_API_URL, E2E_WEB_URL, E2E_DB_CONNECTION_STRING, E2E_DB_HOST, E2E_DB_PORT, E2E_DB_USER, E2E_DB_PASSWORD, E2E_DB_NAME } =
  process.env;

if (!E2E_API_URL || !E2E_WEB_URL || !E2E_DB_CONNECTION_STRING || !E2E_DB_NAME) {
  console.error("[start-backend] playwright.config.ts から起動してください(環境変数が不足しています)。");
  process.exit(1);
}

// 誤って開発用DB等を削除しないための安全装置
if (!/^[a-z0-9_]+_e2e$/.test(E2E_DB_NAME)) {
  console.error(`[start-backend] DB名 "${E2E_DB_NAME}" は "_e2e" で終わる必要があります。`);
  process.exit(1);
}

const backendDir = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "../../backend");
const apiProjectDir = path.join(backendDir, "src/TaskManagementSystem.Api");

async function recreateDatabase() {
  const client = new pg.Client({
    host: E2E_DB_HOST,
    port: Number(E2E_DB_PORT),
    user: E2E_DB_USER,
    password: E2E_DB_PASSWORD,
    database: "postgres",
  });
  await client.connect();
  try {
    await client.query(`DROP DATABASE IF EXISTS "${E2E_DB_NAME}" WITH (FORCE)`);
    await client.query(`CREATE DATABASE "${E2E_DB_NAME}"`);
  } finally {
    await client.end();
  }
}

function run(command, args, options) {
  const result = spawnSync(command, args, { stdio: "inherit", shell: false, ...options });
  if (result.status !== 0) {
    console.error(`[start-backend] コマンドが失敗しました: ${command} ${args.join(" ")}`);
    process.exit(result.status ?? 1);
  }
}

try {
  await recreateDatabase();
} catch (error) {
  console.error("[start-backend] テスト用DBを作成できませんでした。docker compose up -d でDBを起動してください。");
  console.error(error);
  process.exit(1);
}

run("dotnet", ["tool", "restore"], { cwd: backendDir });
run(
  "dotnet",
  [
    "ef", "database", "update",
    "--project", apiProjectDir,
    "--startup-project", apiProjectDir,
    "--configuration", "Release",
    "--connection", E2E_DB_CONNECTION_STRING,
  ],
  { cwd: backendDir },
);

const server = spawn(
  "dotnet",
  ["run", "--configuration", "Release", "--no-build", "--no-launch-profile", "--urls", E2E_API_URL],
  {
    // appsettings.json を読み込むため、プロジェクトディレクトリをカレントにする
    cwd: apiProjectDir,
    stdio: "inherit",
    env: {
      ...process.env,
      ASPNETCORE_ENVIRONMENT: "Development",
      ConnectionStrings__DefaultConnection: E2E_DB_CONNECTION_STRING,
      Cors__AllowedOrigins__0: E2E_WEB_URL,
    },
  },
);

for (const signal of ["SIGINT", "SIGTERM"]) {
  process.on(signal, () => server.kill(signal));
}
server.on("exit", (code) => process.exit(code ?? 0));
