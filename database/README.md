# database

ローカル開発用PostgreSQL（Docker）関連ファイル。

スキーマ管理はEF Core Migrations（`backend/src/TaskManagementSystem.Api/Data/Migrations`）に一本化している。
`docker-compose.yml`はテーブルを作らない素のPostgreSQLコンテナのみを起動する。

```text
database/
├── README.md
└── seed/
    └── 001_seed_data.sql   ローカル動作確認用のサンプルデータ（マイグレーション適用後に手動投入）
```

DDLの設計書（正本）は [docs/database/dll.sql](../docs/database/dll.sql) および
[docs/database/er-diagram.md](../docs/database/er-diagram.md)。
実際のスキーマ作成・変更は必ずEF Core Migrationsを追加する形で行い、
設計を変えた場合はこれらのドキュメントも合わせて更新する。

## セットアップ手順

### 1. PostgreSQLコンテナ起動

```bash
docker compose up -d
```

### 2. EF Core Migration適用

```bash
cd backend
dotnet tool restore
dotnet ef database update --project src/TaskManagementSystem.Api --startup-project src/TaskManagementSystem.Api
```

### 3. （任意）サンプルデータ投入

```bash
docker exec -i task-management-db psql -U postgres -d task_management < database/seed/001_seed_data.sql
```

投入されるテストアカウント（パスワードは全員共通）:

| email | password |
| --- | --- |
| admin@example.com | Password123! |
| yamada@example.com | Password123! |
| suzuki@example.com | Password123! |

## スキーマを作り直す場合

```bash
docker compose down -v
docker compose up -d
cd backend
dotnet ef database update --project src/TaskManagementSystem.Api --startup-project src/TaskManagementSystem.Api
```

## 接続情報（デフォルト）

| 項目 | 値 |
| --- | --- |
| Host | localhost |
| Port | 5432 |
| Database | task_management |
| User | postgres |
| Password | postgres |

`.env` を作成することで上書き可能（[.env.example](../.env.example) 参照）。
