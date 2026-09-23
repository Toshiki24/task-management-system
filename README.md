# 案件・タスク管理システム (Task Management System)

プロジェクト単位でタスクを管理するWebアプリケーションです。

要件定義・基本設計・DB設計・API設計・画面設計という設計工程を経てから実装し、実装後は設計書との差異を照合・修正するところまで行っています。個人開発のポートフォリオとして、Webアプリケーション開発の一連の工程（設計→実装→照合・テスト→デプロイ）を経験することを目的に制作しています。

> **現在の状況**: MVP機能の実装が完了し、設計書との照合・不具合修正まで完了しています。[テスト項目書](design/test-items.md)を作成済みですが、記載した項目を1件ずつ実施してOK/NGを記録する作業はこれから行います。AWSへの本番デプロイも未実施です。

---

## 目次

- [できること（MVP機能）](#できることmvp機能)
- [技術スタック](#技術スタック)
- [アーキテクチャ](#アーキテクチャ)
- [ディレクトリ構成](#ディレクトリ構成)
- [ローカル開発環境の構築](#ローカル開発環境の構築)
- [設計ドキュメント](#設計ドキュメント)
- [開発の進め方](#開発の進め方)
- [今後の予定](#今後の予定)

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
| フロントエンド | Next.js (App Router) / TypeScript / React / Tailwind CSS |
| バックエンド | C# / ASP.NET Core Web API |
| ORM | Entity Framework Core（Code First / Migrations） |
| データベース | PostgreSQL |
| 認証 | JWT（BCryptによるパスワードハッシュ化） |
| ローカルDB環境 | Docker / Docker Compose |
| クラウド（予定） | AWS（Amplify / API Gateway / Lambda / RDS） |
| ソース管理 | Git / GitHub（Pull Requestベースの開発） |

---

## アーキテクチャ

```text
[Browser]
    │
    ▼
[Next.js / TypeScript]
    │  REST API (HTTPS)
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

---

## ディレクトリ構成

```text
task-management-system/
├── backend/            ASP.NET Core Web API
│   └── src/TaskManagementSystem.Api/
│       ├── Controllers/
│       ├── Services/
│       ├── Models/          Entity
│       ├── Dtos/             リクエスト/レスポンス
│       └── Data/             DbContext / Migrations
├── frontend/           Next.js (App Router)
│   └── src/
│       ├── app/               画面
│       ├── components/        共通コンポーネント
│       ├── lib/                APIクライアント等
│       └── types/
├── database/           ローカルDB用シードデータ
├── design/             基本設計書・API仕様書・画面設計書・テスト項目書
├── docs/               要件定義書・DB設計書（ER図・DDL）
└── docker-compose.yml  ローカルPostgreSQL
```

---

## ローカル開発環境の構築

### 前提

- Docker Desktop
- .NET SDK 8.0
- Node.js（npm）

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

`http://localhost:3000` にアクセスするとログイン画面が表示されます。

より詳しいDB周りの手順は [database/README.md](database/README.md) を参照してください。

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
| [テスト項目書](design/test-items.md) | 単体/API/結合/画面/システムテストの項目一覧（実施中） |

---

## 開発の進め方

以下の順序で進めています（[基本設計書 §26](design/basic-design.md) 準拠）。

```text
要件定義 → 基本設計 → DB設計・ER図 → API詳細設計 → 画面詳細設計
   → 開発環境構築 → バックエンド実装 → フロントエンド実装
   → 設計書との照合・不具合修正 ← 現在ここ
   → テスト実施 → AWS環境構築 → 本番デプロイ → README整備
```

機能単位でブランチを作成し、Pull Requestでのレビュー・マージを経てmainに統合しています。実装後に設計書と実装内容を突き合わせて照合し、見つかった差異（バグ・仕様漏れ）を修正する工程も行いました。

---

## 今後の予定

- [ ] [テスト項目書](design/test-items.md)に基づくテストの実施（結果の記録）
- [ ] AWS環境構築・本番デプロイ（Amplify / API Gateway / Lambda / RDS）
- [ ] Phase 2機能（タスク検索・絞り込み、ダッシュボード、タスクステータス履歴、詳細な権限管理）
