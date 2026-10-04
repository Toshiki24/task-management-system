import { GetSecretValueCommand, SecretsManagerClient } from "@aws-sdk/client-secrets-manager";

/**
 * BFF の秘密情報(SESSION_SECRET / ORIGIN_VERIFY_SECRET)の読み込み。
 *
 * 本番(Amplify SSR): 環境変数 `BFF_SECRET_ID` のシークレットを、SSR 実行ロールで Secrets Manager から
 * 実行時に読み込む(秘密情報を環境変数・ビルド成果物に置かない。aws-architecture.md 4.7・4.8)。
 * ローカル開発/E2E: `BFF_SECRET_ID` が未設定のため、従来どおり環境変数から読み込む。
 *
 * サーバー(Route Handler)専用。クライアントコンポーネントから import しないこと。
 */
interface BffSecrets {
  sessionSecret: string;
  originVerifySecret: string | undefined;
}

// SSR(Lambda)はリクエスト間でモジュールを再利用するため、一度取得したら使い回す。
// 取得に失敗した場合は次回再試行できるようキャッシュを破棄する。
let cached: Promise<BffSecrets> | undefined;

async function loadFromSecretsManager(secretId: string): Promise<BffSecrets> {
  const client = new SecretsManagerClient({});
  const response = await client.send(new GetSecretValueCommand({ SecretId: secretId }));
  if (!response.SecretString) {
    throw new Error("BFF用シークレットの値を取得できませんでした。");
  }

  const parsed = JSON.parse(response.SecretString) as Record<string, string | undefined>;
  return {
    sessionSecret: parsed.SESSION_SECRET ?? "",
    originVerifySecret: parsed.ORIGIN_VERIFY_SECRET,
  };
}

function loadFromEnv(): BffSecrets {
  return {
    sessionSecret: process.env.SESSION_SECRET ?? "",
    originVerifySecret: process.env.ORIGIN_VERIFY_SECRET,
  };
}

function getBffSecrets(): Promise<BffSecrets> {
  if (!cached) {
    const secretId = process.env.BFF_SECRET_ID;
    cached = (secretId ? loadFromSecretsManager(secretId) : Promise.resolve(loadFromEnv())).catch((error) => {
      cached = undefined;
      throw error;
    });
  }
  return cached;
}

/** セッションCookieの暗号化鍵。32文字未満・未設定ならエラーにする。 */
export async function getSessionSecret(): Promise<string> {
  const { sessionSecret } = await getBffSecrets();
  if (!sessionSecret || sessionSecret.length < 32) {
    throw new Error(
      "SESSION_SECRET が未設定か短すぎます。32文字以上を、本番は BFF_SECRET_ID のシークレットに、ローカルは環境変数に設定してください。",
    );
  }
  return sessionSecret;
}

/** BFFからAPIへ付与する共有シークレット(X-Origin-Verify)。未設定なら undefined(付与しない)。 */
export async function getOriginVerifySecret(): Promise<string | undefined> {
  return (await getBffSecrets()).originVerifySecret;
}
