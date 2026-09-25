/**
 * E2Eテストの接続先設定。
 *
 * 開発用のサーバー(フロント3000 / API5000)やDB(task_management)とは別に、
 * テスト専用のポート・データベースでアプリを起動するため、手元の動作確認データには影響しない。
 * いずれも環境変数で上書きできる。
 */
export const API_PORT = Number(process.env.E2E_API_PORT ?? 5100);
export const WEB_PORT = Number(process.env.E2E_WEB_PORT ?? 3100);

export const API_URL = `http://localhost:${API_PORT}`;
export const WEB_URL = `http://localhost:${WEB_PORT}`;

export const DB_CONFIG = {
  host: process.env.POSTGRES_HOST ?? "localhost",
  port: Number(process.env.POSTGRES_PORT ?? 5432),
  user: process.env.POSTGRES_USER ?? "postgres",
  password: process.env.POSTGRES_PASSWORD ?? "postgres",
  // テスト開始時に削除・再作成するため、必ず "_e2e" で終わるテスト専用の名前にする
  database: process.env.E2E_DB_NAME ?? "task_management_e2e",
};

export const DB_CONNECTION_STRING =
  `Host=${DB_CONFIG.host};Port=${DB_CONFIG.port};Database=${DB_CONFIG.database};` +
  `Username=${DB_CONFIG.user};Password=${DB_CONFIG.password}`;
