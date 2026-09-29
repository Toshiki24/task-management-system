# 案件・タスク管理システム 基本設計書

## 1. 文書情報

| 項目     | 内容                 |
| ------ | ------------------ |
| 文書名    | 案件・タスク管理システム 基本設計書 |
| バージョン  | 1.9                |
| 作成日    | 2026-09-22         |
| 更新日    | 2026-09-29         |
| 対象システム | 案件・タスク管理システム       |
| 前提文書   | 要件定義書              |

## 改訂履歴

| バージョン | 日付         | 内容                                                                  |
| ----- | ---------- | ------------------------------------------------------------------- |
| 1.0   | 2026-09-22 | 初版作成                                                                |
| 1.1   | 2026-09-28 | セキュリティ見直しに伴い、3.2 AWS構成、4 技術構成、14 認証設計、17 セキュリティ設計、24 ドキュメント構成を更新（詳細は `security-review.md`） |
| 1.2   | 2026-09-28 | 認可チェックの実装に伴い、7.2 プロジェクト内権限に操作可否を追加。25.6 セキュリティテストを追加 |
| 1.3   | 2026-09-28 | Next.js 15.5.26・Node.js 24 への固定に伴い、4.1 バージョン方針を実装に合わせて更新 |
| 1.4   | 2026-09-28 | リフレッシュトークンAPIの実装に伴い、14 認証設計の移行状況の注記を更新 |
| 1.5   | 2026-09-28 | BFFへの移行完了に伴い、3.2・14 の移行状況の注記を削除し、6.1 フロントエンドの責務にBFFを追加 |
| 1.6   | 2026-09-28 | 7.2 プロジェクト内権限に、最後のOWNERの保護とメンバー削除時の担当タスクの扱いを追加 |
| 1.7   | 2026-09-28 | 23.3 CI、25.7 自動テストの実行環境を追加 |
| 1.8   | 2026-09-29 | 実装に合わせて修正。12 API設計方針に、記載のパスがバックエンドAPIの実体パスであり、ブラウザはBFF経由でアクセスする旨を追記し、不足していたエンドポイント（リフレッシュ、ログアウト、ユーザー、プロジェクトメンバー）を追加。7.1 にMVPではシステムロールを保持しない旨を追記 |
| 1.9   | 2026-09-29 | 3.2 AWS構成に、APIをLambdaのコンテナイメージで動かす方法と、秘密の値をSecrets Managerから読み込む方法を追記 |

---

# 2. システム概要

本システムは、プロジェクト単位で案件・タスクを管理するWebアプリケーションである。

プロジェクトの作成、メンバー管理、タスク管理、コメント管理などの基本機能を提供し、プロジェクトの進捗およびタスク状況を一元管理できるようにする。

本システムは、以下の技術を使用したWebアプリケーションとして構築する。

* Next.js
* TypeScript
* React
* C#
* ASP.NET Core Web API
* Entity Framework Core
* LINQ
* PostgreSQL
* Docker
* AWS
* Git / GitHub

---

# 3. システム構成

## 3.1 全体構成

```text
ブラウザ
   │
   ▼
Next.js / TypeScript
   │
   │ HTTPS / REST API
   ▼
ASP.NET Core Web API
   │
   ▼
Entity Framework Core / LINQ
   │
   ▼
PostgreSQL
```

## 3.2 AWS構成

```text
AWS
│
├── Amplify（Hosting compute / SSR）
│     └── Next.js（画面 ＋ BFF）
│
├── API Gateway
│     │
│     ▼
├── Lambda
│     └── ASP.NET Core Web API
│
├── RDS
│     └── PostgreSQL
│
└── CloudWatch
      └── ログ
```

Next.js は Amplify Hosting の SSR（Amplify Hosting compute）で動作させ、Route Handler を BFF（Backend for Frontend）として利用する。ブラウザは Next.js とのみ通信し、API Gateway へは BFF 経由でアクセスする。

```text
ブラウザ
   │  HttpOnly セッションCookie
   ▼
Next.js（BFF）
   │  Authorization: Bearer {JWT}
   ▼
API Gateway / Lambda（ASP.NET Core Web API）
```

BFF 構成を採用した理由は `security-review.md` 6.1 を参照。

**API（Lambda）の実行方法**

* ASP.NET Core Web API をコンテナイメージ（ベースイメージ `public.ecr.aws/lambda/dotnet:8`）として Lambda で動かす。`Amazon.Lambda.AspNetCoreServer.Hosting` が API Gateway（HTTP API）からのイベントを ASP.NET Core のリクエストに変換するため、Controller 等のアプリのコードは Lambda 用に変更しない（実装: `backend/Dockerfile`、`backend/src/TaskManagementSystem.Api/Program.cs`）。
* 署名鍵（`Jwt:Key`）・DB接続文字列などの秘密の値は Secrets Manager に保存し、Lambda の環境変数 `APP_SECRET_ID` で指定したシークレット（設定キーと値のJSON）を起動時に読み込む。リポジトリ・Lambda の環境変数・Terraform の state には秘密の値を置かない（実装: `Configuration/SecretsManagerConfiguration.cs`）。
* 開発用の署名鍵を含む `appsettings.Development.json` はコンテナイメージに含めない。

---

# 4. 技術構成

| 分類            | 技術                           |
| ------------- | ---------------------------- |
| フロントエンド       | Next.js / TypeScript / React |
| バックエンド        | C# / ASP.NET Core Web API    |
| ORM           | Entity Framework Core        |
| データアクセス       | LINQ                         |
| DB            | PostgreSQL                   |
| ローカルDB        | Docker                       |
| クラウド          | AWS                          |
| フロントエンドホスティング | AWS Amplify                  |
| API           | Amazon API Gateway           |
| サーバー処理        | AWS Lambda                   |
| DB            | Amazon RDS for PostgreSQL    |
| ログ            | Amazon CloudWatch            |
| ソース管理         | Git / GitHub                 |

## 4.1 バージョン方針

| 技術      | バージョン              | 理由                                                                                  |
| ------- | ------------------ | ----------------------------------------------------------------------------------- |
| Next.js | 15.5.26（固定）       | Amplify Hosting の SSR の公式対応範囲が Next.js 12〜15 のため（2026-09-28 時点）。詳細は `security-review.md` 6.2 |
| Node.js | 24 系             | Amplify Hosting のサポート対象（20 / 22 / 24）のうち、ローカル開発環境と同じバージョン。Node.js 20 はサポート終了済み（2026年4月） |

Amplify が Next.js 16 に公式対応した時点で、バージョンアップを再検討する。

Amplify はビルド時の Node.js メジャーバージョンに合わせて実行環境を選択するため、`.nvmrc`、`package.json` の `engines`、Amplify のビルド設定で Node.js のバージョンを明示し、ローカル環境と本番環境のバージョンを一致させる。

Next.js 15 が内部で固定している PostCSS（8.4.31）には既知の脆弱性があるため、`frontend/package.json` の `overrides` で修正版（8.5.23以上）に置き換える。Next.js のバージョンを変更する際は、`overrides` が不要になっていないかを `npm audit` で確認する。

---

# 5. アーキテクチャ設計

## 5.1 基本アーキテクチャ

フロントエンドとバックエンドを分離したSPA型のWebアプリケーション構成とする。

```text
[Browser]
    │
    ▼
[Next.js]
    │
    │ REST API
    ▼
[ASP.NET Core Web API]
    │
    ▼
[EF Core / LINQ]
    │
    ▼
[PostgreSQL]
```

## 5.2 バックエンド論理構成

```text
Controller
    │
    ▼
Service
    │
    ▼
DbContext / Repository
    │
    ▼
PostgreSQL
```

ControllerではHTTPリクエストの受付およびレスポンス生成を担当する。

Serviceでは業務ロジックを担当する。

データアクセスではEntity Framework CoreおよびLINQを使用する。

---

# 6. アプリケーション構成

## 6.1 フロントエンド

Next.jsおよびTypeScriptを使用する。

主な責務は以下とする。

* 画面表示
* ユーザー入力受付
* API呼び出し（BFF経由）
* 入力値の簡易チェック
* 認証状態管理
* APIレスポンスの画面反映

また、Route Handler（`src/app/api/bff`）を BFF として使い、以下を担う（3.2 参照）。

* バックエンドAPIへのリクエストの中継（アクセストークンの付与）
* トークンの保管（暗号化Cookie）と、期限が近いアクセストークンの再発行
* CSRF対策（更新系リクエストの `Origin` ヘッダーと `X-Requested-With` ヘッダーの確認）

## 6.2 バックエンド

ASP.NET Core Web APIを使用する。

主な責務は以下とする。

* API受付
* 認証・認可
* 入力値検証
* 業務ロジック
* DBアクセス
* エラーハンドリング

## 6.3 データアクセス

Entity Framework Coreを使用し、LINQによってデータを取得・更新する。

MVPでは基本的にSQLの直接記述やストアドプロシージャを使用せず、EF Core / LINQを中心とする。

---

# 7. ユーザー・権限設計

## 7.1 システム上のユーザー権限

| 権限        | 概要              |
| --------- | --------------- |
| 管理者       | システム全体を管理       |
| プロジェクト管理者 | 担当プロジェクトを管理     |
| メンバー      | プロジェクトおよびタスクを利用 |

MVPでは、上記のシステム上のユーザー権限をDBに保持しない（`users` テーブルにロールの列を持たない）。操作可否は 7.2 のプロジェクト内権限（OWNER / MEMBER）で判定し、「プロジェクト管理者」は OWNER、「メンバー」は MEMBER が相当する。システム管理者ロールはMVP以降の拡張とする（`security-review.md` 5.1。実装: `backend/src/TaskManagementSystem.Api/Models/User.cs`、`Services/ProjectAccess.cs`）。

## 7.2 プロジェクト内権限

| 権限     | 内容         |
| ------ | ---------- |
| OWNER  | プロジェクト管理者  |
| MEMBER | プロジェクトメンバー |

MVPでは、プロジェクトへの所属とプロジェクト内権限（OWNER / MEMBER）によって、以下のとおり操作可否を判定する。

| 操作                     | OWNER | MEMBER | 非メンバー |
| ---------------------- | :---: | :----: | :---: |
| プロジェクト一覧               | 所属分のみ | 所属分のみ  | －     |
| プロジェクト作成               | ○     | ○      | ○     |
| プロジェクト参照               | ○     | ○      | ×（404） |
| プロジェクト編集・削除            | ○     | ×（403） | ×（404） |
| メンバー一覧                 | ○     | ○      | ×（404） |
| メンバー追加・削除              | ○     | ×（403） | ×（404） |
| タスク一覧・参照・作成・編集         | ○     | ○      | ×（404） |
| タスク削除                  | ○     | ×（403） | ×（404） |
| コメント一覧・登録              | ○     | ○      | ×（404） |

* プロジェクト作成は認証済みの全ユーザーに許可し、作成者がOWNERになる。
* 非メンバーがプロジェクト配下のリソースにアクセスした場合は、リソースの存在自体を開示しないため `404` を返す。
* タスクの担当者は、そのプロジェクトのメンバーに限定する。
* プロジェクトには常に1人以上のOWNERが必要とし、最後のOWNERはメンバーから削除できない。
* メンバーを削除した場合、そのプロジェクトでそのユーザーが担当していたタスクは未割り当てにする。
* 要件定義書 10章のシステムロール（管理者等）は、DBにシステムロールを保持しないため、MVPではプロジェクト内権限で代替する。詳細は `security-review.md` 5.1 を参照。

詳細な権限管理（システム管理者ロール、本人が作成したデータのみの操作等）は、MVP以降に拡張する。

---

# 8. 機能構成

要件定義書に定義された機能を以下のように分類する。

| ID  | 機能           | MVP |
| --- | ------------ | --- |
| F01 | ログイン・認証      | ○   |
| F02 | ユーザー管理       | ○   |
| F03 | プロジェクト管理     | ○   |
| F04 | プロジェクトメンバー管理 | ○   |
| F05 | タスク管理        | ○   |
| F06 | タスクコメント      | ○   |
| F07 | タスクステータス履歴   | △   |
| F08 | ダッシュボード      | △   |
| F09 | タスク検索・絞り込み   | △   |

MVPではF01～F06を中心に実装する。

F07～F09はMVP完成後の追加機能として実装する。

---

# 9. 画面構成

| 画面ID    | 画面名      | URL                    | MVP |
| ------- | -------- | ---------------------- | --- |
| SCR-001 | ログイン画面   | `/login`               | ○   |
| SCR-002 | ダッシュボード  | `/dashboard`           | △   |
| SCR-003 | プロジェクト一覧 | `/projects`            | ○   |
| SCR-004 | プロジェクト登録 | `/projects/new`        | ○   |
| SCR-005 | プロジェクト詳細 | `/projects/[id]`       | ○   |
| SCR-006 | タスク一覧    | `/projects/[id]/tasks` | ○   |
| SCR-007 | タスク詳細    | `/tasks/[id]`          | ○   |

※ダッシュボードは要件定義書に従いMVP対象外とする。

---

# 10. 画面設計方針

画面は以下の方針で設計する。

* PCブラウザでの利用を基本とする
* シンプルで操作しやすいUIとする
* 共通レイアウトを使用する
* APIとの通信はREST APIを使用する
* 入力エラーは画面上でユーザーに通知する
* APIエラー発生時は適切なエラーメッセージを表示する

MVPでは、ログイン後にプロジェクト一覧へ遷移できる構成を基本とする。

ダッシュボードは将来的な追加機能として扱う。

---

# 11. データ設計方針

主要テーブルは以下とする。

* users
* projects
* project_members
* tasks
* task_comments
* task_status_histories

各テーブルの詳細な定義については、別途ER図およびDDLで定義する。

---

# 12. API設計方針

REST APIとして設計する。

以下に記載するパスは、**バックエンドAPI（ASP.NET Core）の実体パス**である。ブラウザ（画面）はこれらを直接呼び出さず、Next.js の BFF（Route Handler）を経由してアクセスする（3.2・14章）。BFFのパスは、実体パスの先頭 `/api` を `/api/bff` に置き換えたものとし（例：`GET /api/bff/projects` → `GET /api/projects`）、認証系は BFF 専用の `/api/bff/auth/login`・`/api/bff/auth/logout`・`/api/bff/auth/session` を使う。詳細は API詳細仕様書 6.3・16章を参照（実装: `frontend/src/app/api/bff/`）。

## 12.1 認証

```text
POST /api/auth/login
POST /api/auth/refresh
POST /api/auth/logout
```

`refresh`・`logout` はBFFのみが呼び出し、ブラウザからは中継しない（`/api/bff/auth/refresh` は存在しない）。

## 12.2 プロジェクト

```text
GET    /api/projects
POST   /api/projects
GET    /api/projects/{id}
PUT    /api/projects/{id}
DELETE /api/projects/{id}
```

## 12.3 プロジェクト内タスク

```text
GET  /api/projects/{projectId}/tasks
POST /api/projects/{projectId}/tasks
```

## 12.4 タスク

```text
GET    /api/tasks/{id}
PUT    /api/tasks/{id}
DELETE /api/tasks/{id}
```

## 12.5 コメント

```text
GET  /api/tasks/{taskId}/comments
POST /api/tasks/{taskId}/comments
```

コメントの削除APIは提供しない（将来拡張）。

## 12.6 ユーザー

```text
GET /api/users
GET /api/users/{id}
```

## 12.7 プロジェクトメンバー

```text
GET    /api/projects/{projectId}/members
POST   /api/projects/{projectId}/members
DELETE /api/projects/{projectId}/members/{userId}
```

（12.1〜12.7 の実装: `backend/src/TaskManagementSystem.Api/Controllers/` 配下の各Controller）

ステータス履歴、検索・絞り込み、ダッシュボード用APIについては、該当機能を実装するフェーズで追加する。

---

# 13. APIレスポンス設計

APIレスポンスはJSON形式とする。

成功時の例：

```json
{
  "id": 1,
  "name": "案件管理システム",
  "status": "ACTIVE"
}
```

エラー時はHTTPステータスコードとエラー情報を返却する。

例：

```json
{
  "message": "指定されたプロジェクトが存在しません。"
}
```

---

# 14. 認証設計

JWTを使用した認証方式を採用する。

ログイン成功時にアクセストークン（JWT）とリフレッシュトークンを発行する。トークンは BFF（Next.js）のみが保持し、ブラウザには HttpOnly のセッションCookie のみを渡す。

```text
ログイン
  │
  ▼
認証処理（BFF → API）
  │
  ▼
アクセストークン・リフレッシュトークン発行
  │
  ▼
BFF がトークンを保持し、ブラウザへセッションCookie を発行
  │
  ▼
認証が必要なAPIへアクセス（BFF がアクセストークンを付与）
  │
  ▼
アクセストークン期限切れ時は、BFF がリフレッシュトークンで再発行
```

| 項目         | 方針                                                   |
| ---------- | ---------------------------------------------------- |
| アクセストークン   | JWT。有効期限は15分程度                                       |
| リフレッシュトークン | ランダム値。有効期限は7〜14日。DB にはハッシュ値のみ保存。使用のたびにローテーションする           |
| ログアウト      | リフレッシュトークンを失効させ、セッションCookie を削除する                       |
| セッションCookie | `HttpOnly`、`Secure`、`SameSite=Lax` 以上                |

パスワードは平文で保存せず、ハッシュ化して保存する。

詳細および採用理由は `security-review.md` 5.3、6.1 を参照。

---

# 15. バリデーション設計

以下の入力値についてバリデーションを実施する。

* 必須チェック
* 最大文字数チェック
* メールアドレス形式チェック
* 日付形式チェック
* ステータス値チェック
* 優先度値チェック
* IDの存在チェック

フロントエンドだけでなく、バックエンドでも入力値を検証する。

---

# 16. エラーハンドリング設計

HTTPステータスコードを利用してエラーを分類する。

| HTTPステータス | 用途      |
| --------- | ------- |
| 200       | 正常終了    |
| 201       | 作成成功    |
| 400       | 入力値不正   |
| 401       | 認証エラー   |
| 403       | 権限エラー   |
| 404       | 対象データなし |
| 500       | サーバーエラー |

ユーザーには必要以上に内部情報を公開しない。

---

# 17. セキュリティ設計

以下の対策を実施する。各対策の詳細、現状評価および対応状況は `security-review.md` を参照する。

| 分類      | 対策                                                                    | 参照（security-review.md） |
| ------- | --------------------------------------------------------------------- | --------------------- |
| 認証      | パスワードのハッシュ化（BCrypt）                                                  | 4.1                   |
| 認証      | JWT による認証。アクセストークンの短命化とリフレッシュトークンのローテーション                              | 5.3                   |
| 認証      | ログイン試行回数の制限、ユーザー存在の推測防止                                               | 5.2                   |
| 認可      | プロジェクトへの所属とプロジェクト内権限（OWNER / MEMBER）による認可チェック                           | 5.1                   |
| トークン管理  | トークンは BFF のみが保持し、ブラウザの JavaScript から読み取れないようにする                          | 5.3、6.1               |
| CSRF    | `SameSite` Cookie、`Origin` ヘッダー検証、独自ヘッダーの必須化                           | 5.4                   |
| XSS     | React の自動エスケープ、`dangerouslySetInnerHTML` の禁止、CSP 等のセキュリティヘッダー             | 5.5                   |
| 通信      | HTTPS 通信、本番環境での HSTS                                                  | 5.6                   |
| 入力値     | 入力値検証、EF Core / LINQ による SQL インジェクション対策                                | 4.1                   |
| 情報公開    | エラー時に内部情報を返さない、不要な個人情報（メールアドレス等）を返さない                                 | 4.1、5.6               |
| 秘密情報    | 環境変数による秘密情報管理、AWS認証情報をGitHubへ登録しない、`.env`等の秘密情報をGit管理対象外とする            | －                     |
| 依存ライブラリ | 脆弱性の定期確認                                                              | 5.6                   |

---

# 18. ログ設計

ローカル環境ではコンソールログを利用する。

AWS環境ではCloudWatch Logsを利用する。

主に以下の情報を記録する。

* APIリクエスト
* エラー
* 例外
* システム処理状況

パスワードやJWT等の機密情報はログに出力しない。

---

# 19. 非機能設計

## 19.1 性能

MVPでは小～中規模の利用を想定する。

必要に応じて以下を実施する。

* DBインデックス
* ページング
* APIレスポンスの最適化
* 不要なDBアクセスの削減

## 19.2 可用性

AWSマネージドサービスを利用し、個人開発規模で可能な範囲の可用性を確保する。

## 19.3 保守性

責務を分離し、機能追加しやすい構成とする。

---

# 20. DBアクセス設計

Entity Framework Coreを使用する。

基本的なデータ取得・更新にはLINQを使用する。

例：

```csharp
var tasks = await dbContext.Tasks
    .Where(x => x.ProjectId == projectId)
    .OrderByDescending(x => x.CreatedAt)
    .ToListAsync();
```

MVPではストアドプロシージャを必須としない。

---

# 21. トランザクション設計

複数のDB更新を1つの処理として扱う必要がある場合は、トランザクションを利用する。

例：

* プロジェクト作成とメンバー登録
* タスク更新とステータス履歴登録

ただし、MVPでは必要な処理に限定して利用する。

---

# 22. ローカル開発環境

## 22.1 構成

```text
Next.js
  │
  ▼
ASP.NET Core Web API
  │
  ▼
PostgreSQL
  │
  └── Docker
```

## 22.2 ポート

| サービス         | ポート         |
| ------------ | ----------- |
| Next.js      | 3000        |
| ASP.NET Core | 5000 / 5001 |
| PostgreSQL   | 5432        |

※実装時に最終確定する。

---

# 23. Git / GitHub運用

GitHubをソースコードおよび設計ドキュメントの管理に使用する。

## 23.1 ブランチ

基本ブランチ：

```text
main
```

機能開発時には以下のようなブランチを使用する。

```text
feature/login
feature/project-management
feature/task-management
feature/comment
```

## 23.2 コミット

機能単位でコミットする。

例：

```text
docs: add requirements document
docs: add basic design
feat: implement login API
feat: implement project management
feat: implement task management
```

## 23.3 CI

GitHub Actions（`.github/workflows/ci.yml`）で、プルリクエストの作成・更新時と main への push 時に以下を自動で実行する。依存ライブラリの脆弱性は日々新たに公開されるため、週1回も実行する。

| ジョブ | 内容 |
| --- | --- |
| 単体テスト | `dotnet test`（PostgreSQL をサービスコンテナで起動） |
| E2Eテスト | API・結合・画面・システム・セキュリティテスト（Playwright） |
| フロントエンドの静的解析 | ESLint、TypeScript の型チェック |
| 依存ライブラリの脆弱性確認 | `npm audit`（frontend・e2e）、`dotnet list package --vulnerable`。high以上の脆弱性があれば失敗させる |

Dependabot（`.github/dependabot.yml`）で、npm・NuGet・GitHub Actions の依存関係の更新プルリクエストを週1回作成する。プルリクエストは CI が成功してからマージする。

---

# 24. ドキュメント構成

```text
docs/
├── requirements.md
├── design/
│   ├── basic-design.md
│   ├── api-specification.md
│   ├── screen-design.md
│   └── security-review.md
├── database/
│   ├── er-diagram.md
│   └── ddl.sql
└── architecture/
    └── aws-architecture.md
```

---

# 25. テスト方針

以下のテストを実施する。

## 25.1 単体テスト

Service等の個別ロジックを対象とする。

## 25.2 APIテスト

APIのリクエストおよびレスポンスを確認する。

## 25.3 結合テスト

APIとDBの連携を確認する。

## 25.4 画面テスト

画面操作およびAPI連携を確認する。

## 25.5 システムテスト

ログインからプロジェクト、タスク管理まで一連の操作を確認する。

## 25.6 セキュリティテスト

セキュリティ見直し記録（`security-review.md`）の課題に対する対策（認可チェック等）を確認する。

セキュリティ対応の各ステップ後には、追加したテストとあわせて既存の全テストを回帰テストとして実施する。

## 25.7 自動テストの実行環境

すべてのテストは自動テストとして実装し、ローカル環境と CI（23.3）の両方で実行できるようにする。プルリクエストは CI のテストが成功してからマージする。

---

# 26. 開発フェーズ

以下の順序で開発する。

```text
1. 要件定義
      ↓
2. 基本設計
      ↓
3. ER / DB設計
      ↓
4. API詳細設計
      ↓
5. 画面詳細設計
      ↓
6. 開発環境構築
      ↓
7. フロントエンド実装
      ↓
8. バックエンド実装
      ↓
9. DB実装
      ↓
10. テスト
      ↓
11. AWSデプロイ
      ↓
12. README整備
```

---

# 27. MVP実装範囲

MVPでは以下を実装する。

## 27.1 認証

* ログイン
* JWT発行
* 認証チェック

## 27.2 ユーザー

* ユーザー情報管理

## 27.3 プロジェクト

* プロジェクト一覧
* プロジェクト登録
* プロジェクト詳細
* プロジェクト編集
* プロジェクト削除

## 27.4 メンバー

* プロジェクトへのメンバー追加
* メンバー一覧表示

## 27.5 タスク

* タスク登録
* タスク一覧
* タスク詳細
* タスク編集
* タスク削除
* 担当者設定
* ステータス設定
* 優先度設定
* 期限設定

## 27.6 コメント

* コメント登録
* コメント一覧

---

# 28. MVP以降の追加機能

MVP完成後、以下の順序で機能を拡張する。

## Phase 2

* タスク検索
* タスク絞り込み
* ページング
* タスクステータス履歴
* ダッシュボード

## Phase 3

* カンバン表示
* カレンダー表示
* ファイル添付
* 通知機能

## Phase 4

* Slack / Teams連携
* Google Calendar連携
* AIによるタスク分類・補助

## Phase 5

* CI/CD
* 自動テスト拡充
* AWS構成最適化
* パフォーマンス改善

---

# 29. 設計上の留意事項

## 29.1 MVPと追加機能の分離

MVPでは必要最低限の機能に絞り、開発期間を抑える。

特に以下はMVPに含めない。

* ダッシュボード
* タスク検索・絞り込み
* ステータス履歴

## 29.2 将来拡張

MVP以降の機能追加を考慮し、APIおよびDBは拡張可能な構成とする。

## 29.3 ポートフォリオとしての設計

本システムでは単純なCRUDだけでなく、以下の開発工程を経験できる構成とする。

* 要件定義
* 基本設計
* DB設計
* API設計
* 画面設計
* フロントエンド開発
* バックエンド開発
* テスト
* AWSデプロイ
* GitHubによるバージョン管理

---

# 30. 基本設計完了条件

以下を満たした時点で基本設計を完了とする。

* [ ] システム全体構成が定義されている
* [ ] 技術構成が定義されている
* [ ] アプリケーション構成が定義されている
* [ ] ユーザー権限が定義されている
* [ ] 機能構成が定義されている
* [ ] 画面構成が定義されている
* [ ] 画面遷移が定義されている
* [ ] データ構成の概要が定義されている
* [ ] API構成が定義されている
* [ ] 認証・認可方針が定義されている
* [ ] セキュリティ方針が定義されている
* [ ] エラーハンドリング方針が定義されている
* [ ] テスト方針が定義されている
* [ ] AWS構成方針が定義されている
* [ ] MVPの実装範囲が明確になっている

---

# 31. 次工程

基本設計完了後、以下の設計を進める。

1. ER図作成
2. DBテーブル詳細設計
3. DDL作成
4. API詳細仕様作成
5. 画面詳細設計
6. 開発環境構築
7. 実装開始

次工程では、まず以下を作成する。

```text
docs/database/er-diagram.md
```

その後、

```text
docs/database/ddl.sql
docs/design/api-specification.md
docs/design/screen-design.md
```

を作成する。
