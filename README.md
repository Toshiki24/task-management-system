# 案件・タスク管理システム (Task Management System)

[![CI](https://github.com/Toshiki24/task-management-system/actions/workflows/ci.yml/badge.svg)](https://github.com/Toshiki24/task-management-system/actions/workflows/ci.yml)

プロジェクト単位でタスクを管理するWebアプリケーションです。

要件定義・基本設計・DB設計・API設計・画面設計という設計工程を経てから実装し、実装後は設計書との照合・不具合修正を経て、テスト項目書に基づくテストまで完了しています。個人開発のポートフォリオとして、Webアプリケーション開発の一連の工程（設計→実装→照合・テスト→デプロイ）を経験することを目的に制作しています。

> **現在の状況**: MVP機能の実装、設計書との照合・不具合修正、テストまで完了しています。テストは[テスト項目書](design/test-items.md)の全159項目を自動テスト化して実施し、1回目でNGとなった13項目を修正した上で、2回目の再テストで全項目OKとなりました。
>
> AWSデプロイの前にセキュリティ面を見直し、認可チェックの未実装などの課題を洗い出して、[セキュリティ見直し記録](design/security-review.md)の対応計画に沿ってすべて対応しました（認可チェック、ログイン試行回数の制限、BFF構成とリフレッシュトークンへの移行、CSRF対策、CSP・セキュリティヘッダー等）。テストは全253項目がOKで、GitHub Actions のCIでプルリクエストごとに自動実行しています。その後、AWS環境を Terraform で構築して本番デプロイまで完了し、**[https://tms.accent24.jp](https://tms.accent24.jp)** で公開しています（デモアカウントは [本番環境（デモ）](#本番環境デモ) を参照）。
>
> **現在は Phase 2 を開発中です。** チーム利用に向けて「ワークスペース（チーム/部署）単位の可視性・権限分離」を中心に実装を進めています。進捗は [Phase 2（開発中）](#phase-2開発中) を参照してください。

---

## 本番環境（デモ）

AWS 上に本番環境を構築し、公開しています。構成・設定値・デプロイ手順は [AWS構成設計書](design/aws-architecture.md)、デプロイ時に新たに洗い出したリスクと対策は [セキュリティ見直し記録2](design/security-review-2.md) を参照してください。

- **URL**: <https://tms.accent24.jp>
- **デモアカウント**（ポートフォリオ公開用。いずれもパスワードは同じ）:

  | 名前 | メールアドレス | パスワード |
  | --- | --- | --- |
  | 管理者ユーザー | `admin@example.com` | `Password123!` |
  | 山田太郎 | `yamada@example.com` | `Password123!` |
  | 鈴木花子 | `suzuki@example.com` | `Password123!` |

このシステムは登録APIを持たないため、初期ユーザーは RDS（プライベートサブネット）に到達できるマイグレーション用 Lambda 経由でDBに投入しています。パスワードは BCrypt でハッシュ化して保存し、平文は保存していません。

### 本番構成の概要

| 層 | 構成 |
| --- | --- |
| フロント／BFF | AWS Amplify Hosting（Next.js SSR・WEB_COMPUTE）、独自ドメイン＋HTTPS（ACM） |
| API | AWS Lambda（コンテナイメージ）＋ API Gateway（HTTP API・スロットリング） |
| DB | RDS PostgreSQL（プライベートサブネット・パブリックアクセス無効・TLS必須・最小権限の `tms_app` ユーザー） |
| 秘密情報 | AWS Secrets Manager（git・Terraform state・ビルド成果物には一切含めない） |
| CI/CD | フロント＝Amplify 自動ビルド／バックエンド＝GitHub Actions（OIDC・アクセスキー非保存） |
| 監視・保全 | CloudTrail＋S3、CloudWatch アラーム、SNS 通知、AWS Budgets、AWS Backup（Vault Lock） |

---

## 目次

- [本番環境（デモ）](#本番環境デモ)
- [Phase 2（開発中）](#phase-2開発中)
- [できること（MVP機能）](#できることmvp機能)
- [技術スタック](#技術スタック)
- [アーキテクチャ](#アーキテクチャ)
- [ディレクトリ構成](#ディレクトリ構成)
- [ローカル開発環境の構築](#ローカル開発環境の構築)
- [自動テスト](#自動テスト)
- [設計ドキュメント](#設計ドキュメント)
- [開発の進め方](#開発の進め方)
- [今後の予定](#今後の予定)

---

## Phase 2（開発中）

Phase 1（MVP）の公開後、**チーム利用**に向けて Phase 2 を開発しています。中心となるのは **ワークスペース（チーム/部署）単位の可視性・権限分離** です。設計は「要件定義 → ADR（方式決定）→ 基本設計」の順に固め、実装は**小さな単位の Pull Request** に分け、各PRで単体テスト・E2Eテストをグリーンにしてからマージしています。

- **方式**: 1 インスタンス = 1 組織とし、ワークスペースを**可視性・権限の境界**にする（マルチテナントは採用しない）。ロールは 3 スコープ（**System Admin** ／ ワークスペース **Admin・Member・Viewer** ／ プロジェクト **OWNER・MEMBER**）。
- **関連ドキュメント**: [Phase 2 要件定義](docs/phase2/requirements.md) ／ [ADR 0001 ワークスペース方式](docs/phase2/adr/0001-workspace-model.md) ／ [M1 基本設計](design/phase2/m1-workspace.md)

Phase 2 は、それぞれ**単体で完成・デモ可能**なマイルストーン（M0〜M5）に分割して進めます。実装順は固定で、まず **M1（ワークスペース境界と認可）** を全体へ通してから、その上に UX・協働・Git 連携・見える化を積み上げます。

### 全体の進捗（マイルストーン）

| M | テーマ | 状況 |
| --- | --- | --- |
| **M0** | 下地（ADR、新スコープのテスト基盤 ほか） | 🚧 一部（ADR・テスト基盤は着手済み） |
| **M1** | ワークスペース/チーム土台（可視性・3スコープ認可・招待・System Admin・監査） | 🚧 実装中（バックエンドは完了、画面が残り） |
| **M2** | 日常 UX（カンバン、検索/フィルタ、My Tasks、サブタスク ほか） | ⬜ 未着手 |
| **M3** | 協働・通知・リアルタイム・計画（メンション、通知、サイクル ほか） | ⬜ 未着手 |
| **M4** | Git 連携（GitHub/GitLab・オンプレ・Webhook・自動遷移 ほか） | ⬜ 未着手 |
| **M5** | 見える化・運用・エンタープライズ（指標、配布2系統、SSO/MFA ほか） | ⬜ 未着手 |

到達目標は **M1〜M4** で開発チームが実用的に使える一通りを揃えること（M5 は余力に応じて）。マイルストーンの定義は [Phase 2 要件定義 §10](docs/phase2/requirements.md) を参照。

> Phase 2 は Pull Request（#80〜）として `main` に統合しながら進めています。本番（<https://tms.accent24.jp>）は引き続き Phase 1 の機能で公開しており、Phase 2 の反映は M1 の仕上げ後にまとめて行う予定です。

---

## できること（MVP機能）

| 機能 | 内容 |
| --- | --- |
| ログイン | JWTによる認証 |
| プロジェクト管理 | 一覧・登録・詳細・編集・削除 |
| プロジェクトメンバー管理 | メンバー一覧・追加・削除（プロジェクト作成者は自動的にOWNERとして登録） |
| タスク管理 | 一覧・登録（モーダル）・詳細・編集・削除、担当者/ステータス/優先度/期限の設定 |
| タスクコメント | コメント一覧・投稿 |

対応する画面・APIの詳細は [screen-design.md](design/screen-design.md) / [api-specification.md](design/api-specification.md) を参照してください。

---

## 技術スタック

| 分類 | 技術 |
| --- | --- |
| フロントエンド | Next.js 15 (App Router) / TypeScript / React / Tailwind CSS |
| バックエンド | C# / ASP.NET Core Web API |
| ORM | Entity Framework Core（Code First / Migrations） |
| データベース | PostgreSQL |
| 認証 | JWT＋リフレッシュトークン（BCryptによるパスワードハッシュ化）。BFF（Next.js Route Handler）がトークンを暗号化Cookieで管理 |
| ローカルDB環境 | Docker / Docker Compose |
| クラウド | AWS（Amplify / API Gateway / Lambda / RDS / Secrets Manager / CloudTrail / CloudWatch / Backup）。構成は Terraform で管理（`infra/terraform`） |
| ソース管理 | Git / GitHub（Pull Requestベースの開発） |

---

## アーキテクチャ

```text
[Browser]
    │  HttpOnly のセッションCookie
    ▼
[Next.js / TypeScript]  画面 ＋ BFF（Route Handler）
    │  REST API (HTTPS)  Authorization: Bearer {JWT}
    ▼
[ASP.NET Core Web API]
    │  Controller → Service → DbContext
    ▼
[Entity Framework Core / LINQ]
    │
    ▼
[PostgreSQL]
```

フロントエンドはコンポーネント指向（`components/layout`, `components/common`, `components/project`, `components/task`）、バックエンドはController／Service層で責務を分離しています。詳細は [basic-design.md](design/basic-design.md) を参照してください。

ブラウザはAPIを直接呼び出さず、Next.js の Route Handler で作ったBFFを経由します。アクセストークン・リフレッシュトークンはBFFが暗号化したHttpOnly Cookieに保存し、ブラウザのJavaScriptからは読み取れません。この構成にした理由は [セキュリティ見直し記録](design/security-review.md) を参照してください。

---

## ディレクトリ構成

```text
task-management-system/
├── backend/            ASP.NET Core Web API
│   ├── src/TaskManagementSystem.Api/
│   │   ├── Controllers/
│   │   ├── Services/
│   │   ├── Models/          Entity
│   │   ├── Dtos/             リクエスト/レスポンス
│   │   └── Data/             DbContext / Migrations
│   └── tests/TaskManagementSystem.Api.Tests/   単体テスト（xUnit）
├── frontend/           Next.js (App Router)
│   └── src/
│       ├── app/               画面
│       ├── components/        共通コンポーネント
│       ├── lib/                APIクライアント等
│       └── types/
├── database/           ローカルDB用シードデータ
├── e2e/                APIテスト・結合テスト・画面テスト・システムテスト（Playwright）
├── infra/terraform/    AWS環境の構成（Terraform）
├── design/             基本設計書・API仕様書・画面設計書・テスト項目書
├── docs/               要件定義書・DB設計書（ER図・DDL）
└── docker-compose.yml  ローカルPostgreSQL
```

---

## ローカル開発環境の構築

### 前提

- Docker Desktop
- .NET SDK 8.0
- Node.js 24（npm）。`frontend/.nvmrc` で指定しています

### 1. PostgreSQLの起動

```bash
docker compose up -d
```

### 2. バックエンド（EF Core Migration適用 → 起動）

```bash
cd backend
dotnet tool restore
dotnet ef database update --project src/TaskManagementSystem.Api --startup-project src/TaskManagementSystem.Api
dotnet run --project src/TaskManagementSystem.Api
```

`http://localhost:5000` で起動します（Swagger UIは `/swagger`）。

JWTの署名鍵（`Jwt:Key`）は、ローカル開発では追加の設定は不要です。`dotnet run` は `Properties/launchSettings.json` により Development 環境で起動し、`appsettings.Development.json` にある開発専用の値が使われます。別の値を使いたい場合は、環境変数 `Jwt__Key` に32バイト以上の文字列を設定してから起動してください（未設定・32バイト未満の場合は起動時にエラーになります）。本番環境では `appsettings.json` に値を持たないため、必ず環境変数等で設定します（[セキュリティ見直し記録 §8](design/security-review.md)）。

AWS Lambda 用のコンテナイメージは `backend` ディレクトリで `docker build -t task-management-api .` でビルドできます（本番では Secrets Manager から秘密情報を読み込みます。詳細は [基本設計書 3.2](design/basic-design.md)）。

### 3. （任意）サンプルデータ投入

```bash
docker exec -i task-management-db psql -U postgres -d task_management < database/seed/001_seed_data.sql
```

テストアカウント: `admin@example.com` / `Password123!`（他アカウントは [database/README.md](database/README.md) 参照）

### 4. フロントエンド

```bash
cd frontend
cp .env.example .env.local
npm install
npm run dev
```

`.env.local` には、BFFが呼び出すAPIのURL（`API_BASE_URL`）と、セッションCookieの暗号化鍵（`SESSION_SECRET`、32文字以上）を設定します。詳しくは [frontend/README.md](frontend/README.md) を参照してください。

`http://localhost:3000` にアクセスするとログイン画面が表示されます。

より詳しいDB周りの手順は [database/README.md](database/README.md) を参照してください。

---

## 自動テスト

[テスト項目書](design/test-items.md)の全項目を自動テストとして実装しています。テスト名の先頭がテスト項目書のNo.（`UT-101`、`SCR-005-03`など）に対応しています。

| 分類 | 実装先 | ツール |
| --- | --- | --- |
| 単体テスト (UT) | `backend/tests/TaskManagementSystem.Api.Tests` | xUnit |
| APIテスト (API) | `e2e/tests/api` | Playwright |
| 結合テスト (IT) | `e2e/tests/integration` | Playwright + pg（node-postgres。DBを直接参照して確認） |
| 画面テスト (SCR) | `e2e/tests/screen` | Playwright |
| システムテスト (ST) | `e2e/tests/system` | Playwright |
| セキュリティテスト (SEC) | `e2e/tests/security` | Playwright |

### 実行方法

PostgreSQLコンテナ（`docker compose up -d`）が起動していれば実行できます。開発用のバックエンド・フロントエンドを起動しておく必要はありません。

```bash
# 単体テスト
cd backend
dotnet test

# API・結合・画面・システムテスト（初回のみ npm install と npx playwright install chromium が必要）
cd e2e
npm test
```

分類ごとの実行は `npm run test:api` / `test:integration` / `test:screen` / `test:system` / `test:security`、結果レポートは `npm run report` で確認できます。

### CI（GitHub Actions）

プルリクエストの作成・更新時と main への push 時に、単体テスト・E2Eテスト・ESLint と型チェック・依存ライブラリの脆弱性確認（`npm audit`、`dotnet list package --vulnerable`）を自動で実行します（`.github/workflows/ci.yml`）。脆弱性確認は週1回も実行し、Dependabot が依存ライブラリの更新プルリクエストを作成します。

### テストデータの扱い

- **開発用DB（`task_management`）には接続しません。** 単体テストはテストクラスごとに使い捨てのDB（`task_management_ut_<GUID>`）を作成して終了時に削除し、E2Eテストは実行のたびに `task_management_e2e` を作り直します。
- E2Eテストでは、アプリをテスト専用のポート（API: 5100、フロントエンド: 3100）で起動します。開発サーバー（5000 / 3000）と同時に動かしても干渉しません。
- 各テストは自分専用のユーザー・プロジェクトを作成して使い、終了時に削除します。他のテストのデータには触れないため、並列実行しても結果が変わりません。
- 件数や全体の状態に依存する項目（プロジェクト0件表示、API取得失敗など）は、実データを消したりバックエンドを止めたりせず、その画面の通信だけを差し替えて確認します。

---

## 設計ドキュメント

開発工程で作成した設計書一式です。実装より設計を先に固めてから着手しています。

| ドキュメント | 内容 |
| --- | --- |
| [要件定義書](docs/requirements.md) | システムの目的・機能要件・非機能要件・MVP範囲 |
| [基本設計書](design/basic-design.md) | システム構成・アーキテクチャ・技術構成・テスト方針 |
| [ER図・DB設計](docs/database/er-diagram.md) / [DDL](docs/database/dll.sql) | テーブル定義・リレーション |
| [API詳細仕様書](design/api-specification.md) | 全APIのリクエスト/レスポンス・バリデーション・エラー仕様 |
| [画面詳細設計書](design/screen-design.md) | 画面レイアウト・入力項目・バリデーション・コンポーネント方針 |
| [テスト項目書](design/test-items.md) | 単体/API/結合/画面/システムテストの項目一覧と実施結果 |
| [セキュリティ見直し記録](design/security-review.md) | デプロイ前のセキュリティ見直しで見つかった課題・対策・設計判断・対応計画 |
| [AWS構成設計書](design/aws-architecture.md) | 本番環境（AWS）の構成・設定値、セキュリティとコストの設計、デプロイ手順 |

### Phase 2（開発中）のドキュメント

| ドキュメント | 内容 |
| --- | --- |
| [Phase 2 要件定義](docs/phase2/requirements.md) | チーム利用に向けた機能要件・非機能要件・マイルストーン |
| [ADR 0001 ワークスペース方式](docs/phase2/adr/0001-workspace-model.md) | マルチテナントを採用せずワークスペース方式を選んだ理由（意思決定記録） |
| [M1 基本設計](design/phase2/m1-workspace.md) | データモデル・3スコープ認可・招待・System Admin・API/画面への影響 |

---

## 開発の進め方

以下の順序で進めています（[基本設計書 §26](design/basic-design.md) 準拠）。

```text
要件定義 → 基本設計 → DB設計・ER図 → API詳細設計 → 画面詳細設計
   → 開発環境構築 → バックエンド実装 → フロントエンド実装
   → 設計書との照合・不具合修正 → テスト実施
   → セキュリティ見直し・対応
   → AWS環境構築（Terraform） → 本番デプロイ → README整備 ← Phase 1 完了
   → 【Phase 2】要件定義 → ADR → 基本設計 → 実装（PR単位） ← 現在ここ
```

機能単位でブランチを作成し、Pull Requestでのレビュー・マージを経てmainに統合しています。実装後に設計書と実装内容を突き合わせて照合し、見つかった差異（バグ・仕様漏れ）を修正する工程も行いました。

---

## 今後の予定

- [x] [テスト項目書](design/test-items.md)に基づくテストの自動化・1回目の実施（結果の記録）
- [x] 1回目でNGとなった項目の修正・再テスト（2回目、全項目OK）
- [x] セキュリティ見直し（課題の洗い出し・対策方針・対応計画の策定。[セキュリティ見直し記録](design/security-review.md)）
- [x] セキュリティ対応（認可チェック、ログイン保護、BFF構成への移行、CSP等。[セキュリティ見直し記録 §7](design/security-review.md)）
- [x] CI（GitHub Actions）によるテスト・依存ライブラリの脆弱性確認の自動化
- [x] AWS環境構築（Terraform）・本番デプロイ（Amplify / API Gateway / Lambda / RDS）。デプロイ時に新たに洗い出したリスクと対策は[セキュリティ見直し記録2](design/security-review-2.md)を参照
- [x] 本番公開（<https://tms.accent24.jp>）・README整備
- [x] SEC2-10（CSP の nonce 化によるインラインスクリプト制限の強化）。本番（Amplify SSR）の middleware で実装・動作確認済み（[セキュリティ見直し記録2](design/security-review-2.md)）
- [ ] **Phase 2（開発中）**: チーム利用に向けた機能群。マイルストーン単位で進行（詳細と進捗は [Phase 2（開発中）](#phase-2開発中) を参照）
  - [ ] M1 ワークスペース土台（可視性・3スコープ認可・招待・System Admin・監査）… 🚧 実装中（バックエンド完了、画面が残り）
  - [ ] M2 日常 UX（カンバン・検索/フィルタ・My Tasks ほか）
  - [ ] M3 協働・通知・リアルタイム・計画
  - [ ] M4 Git 連携（GitHub/GitLab・オンプレ・Webhook）
  - [ ] M5 見える化・運用・エンタープライズ
