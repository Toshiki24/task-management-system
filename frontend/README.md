# frontend

案件・タスク管理システムのフロントエンド（Next.js App Router / TypeScript / React / Tailwind CSS）です。

システム全体の概要とセットアップ手順は [ルートのREADME](../README.md) を参照してください。

## バージョン

| 技術 | バージョン | 備考 |
| --- | --- | --- |
| Node.js | 24系 | `.nvmrc` と `package.json` の `engines` で指定 |
| Next.js | 15.5.26（固定） | デプロイ先の Amplify Hosting の SSR が Next.js 15 までの対応のため（[基本設計書 4.1](../design/basic-design.md)、[セキュリティ見直し記録 6.2](../design/security-review.md)） |

Next.js 15 が内部で使用する PostCSS 8.4.31 には既知の脆弱性があるため、`package.json` の `overrides` で修正版（8.5.23以上）に置き換えています。

## 開発サーバーの起動

バックエンド（`http://localhost:5000`）を起動した状態で実行します。

```bash
nvm use
cp .env.example .env.local
npm install
npm run dev
```

`http://localhost:3000` でログイン画面が表示されます。

## 環境変数

いずれもサーバー側（BFF）でのみ使用し、ブラウザには公開しません。

| 変数名 | 内容 | 例 |
| --- | --- | --- |
| `API_BASE_URL` | BFFが呼び出すバックエンドAPIのベースURL | `http://localhost:5000/api` |
| `SESSION_SECRET` | セッションCookieの暗号化鍵（32文字以上のランダムな文字列）。変更すると全ユーザーがログアウトされる | `node -e "console.log(require('crypto').randomBytes(32).toString('hex'))"` で生成 |

## BFF（`src/app/api/bff`）

画面からのAPI呼び出しは、すべて同一オリジンのBFF（Next.js の Route Handler）を経由します。

| パス | 内容 |
| --- | --- |
| `auth/login` | ログイン。トークンを暗号化Cookie（`tms_session`）に保存し、ユーザー情報だけを返す |
| `auth/logout` | ログアウト。リフレッシュトークンを失効させ、Cookieを削除する |
| `auth/session` | ログイン中のユーザー情報を返す |
| `[...path]` | Cookie内のアクセストークンを付けてAPIへ中継する（期限が近ければ自動で再発行） |

更新系リクエストには、CSRF対策として同一オリジンの `Origin` ヘッダーと `X-Requested-With: XMLHttpRequest` ヘッダーが必要です（`src/lib/api.ts` が付与します）。詳細は [API詳細仕様書 6.3](../design/api-specification.md) を参照してください。

## スクリプト

| コマンド | 内容 |
| --- | --- |
| `npm run dev` | 開発サーバーを起動する |
| `npm run build` | 本番用にビルドする |
| `npm run start` | ビルド結果を起動する |
| `npm run lint` | ESLint を実行する |

画面・API連携のテストは `e2e/`（Playwright）にあります。実行方法はルートのREADMEの「自動テスト」を参照してください。
