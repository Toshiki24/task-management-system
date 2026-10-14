# 自己ホスト手順（Docker Compose）

AWS に依存せず、**API + フロント/BFF + PostgreSQL** を Docker Compose で起動する手順です（Phase 2 M5 §8）。
AWS 本番構成（API Gateway + Lambda / Amplify / RDS / Secrets Manager）とは独立しており、同じアプリを自前のサーバーで動かせます。

- 関連: [M5 基本設計 §8](../design/phase2/m5-visibility-ops.md) / [Phase 2 要件定義 §8 運用形態](phase2/requirements.md)
- 構成ファイル: `docker-compose.selfhost.yml` / `backend/Dockerfile.selfhost` / `frontend/Dockerfile` / `.env.selfhost.example`

---

## 1. 前提

- Docker Engine と Docker Compose v2（`docker compose` サブコマンド）
- TLS を終端するリバースプロキシ（Caddy / nginx / Traefik 等）。
  セッション Cookie は本番モードで `Secure` 属性が付くため、**ブラウザからは HTTPS 経由でアクセス**してください。
  （`http://localhost:3000` に直接アクセスするとログイン Cookie が保存されずログインできません）

## 2. 構成

```
[ブラウザ] ──HTTPS──> [リバースプロキシ(TLS終端)] ──HTTP──> web (Next.js/BFF :3000)
                                                              │  （BFF が内部でAPIへ）
                                                              ▼
                                                         api (ASP.NET Core :8080)
                                                              │
                                                              ▼
                                                         db  (PostgreSQL :5432)
```

- `web` だけを外部公開します。`api` と `db` は Compose の内部ネットワークに閉じます。
- マイグレーションは `api` の起動時に自動適用されます（`RUN_MIGRATIONS_ON_STARTUP=true`。再実行しても安全）。

## 3. 起動

```bash
# 1) 環境変数ファイルを用意し、秘密情報を設定する（32文字以上を推奨: openssl rand -base64 36）
cp .env.selfhost.example .env.selfhost
$EDITOR .env.selfhost

# 2) ビルドして起動
docker compose --env-file .env.selfhost -f docker-compose.selfhost.yml up -d --build

# 3) 状態確認（api / web が healthy になるまで待つ）
docker compose --env-file .env.selfhost -f docker-compose.selfhost.yml ps
```

必須の秘密情報（`.env.selfhost`）:

| 変数 | 用途 |
|------|------|
| `POSTGRES_PASSWORD` | DB パスワード |
| `JWT_KEY` | アクセストークンの署名鍵（32文字以上） |
| `ORIGIN_VERIFY_SECRET` | BFF→API 間の呼び出し検証（api と web で同一値） |
| `SESSION_SECRET` | セッション Cookie の暗号化キー（32文字以上） |
| `MFA_KEY` | （任意）TOTP シークレットの暗号化鍵。未設定なら `JWT_KEY` を流用 |

## 4. 初期管理者の作成

自己ホストには利用者登録 API はありません。最初の System Admin は、初回起動時に環境変数で冪等に作成できます。

```bash
# .env.selfhost に設定して起動（既に存在すれば何もしない）
SEED_ADMIN_EMAIL=admin@example.com
SEED_ADMIN_PASSWORD=（十分に強いパスワード）
SEED_ADMIN_NAME=管理者
```

作成後はこの3変数を外して再デプロイして構いません。**以降のユーザー追加は、管理者による招待フロー（M1）** で行います
（管理コンソール／ワークスペース設定からメール招待 → 被招待者がトークンで参加）。

## 5. ヘルスチェック

- API: `GET /health` が `{"status":"ok"}` を返します（認証・オリジン検証なしで到達可能、秘密情報なし）。
- Compose の healthcheck で `api`（curl）と `web`（Node fetch で `/login`）の死活を監視します。

```bash
# コンテナ内から
docker compose --env-file .env.selfhost -f docker-compose.selfhost.yml exec api curl -fsS http://localhost:8080/health
```

## 6. バックアップ / リストア

PostgreSQL のデータは名前付きボリューム `db_data` に保存されます。論理バックアップの例:

```bash
# バックアップ（カスタム形式）
docker compose --env-file .env.selfhost -f docker-compose.selfhost.yml exec -T db \
  pg_dump -U "$POSTGRES_USER" -F c "$POSTGRES_DB" > backup_$(date +%Y%m%d_%H%M%S).dump

# リストア（既存DBへ上書き投入。--clean で既存オブジェクトを削除してから復元）
docker compose --env-file .env.selfhost -f docker-compose.selfhost.yml exec -T db \
  pg_restore -U "$POSTGRES_USER" -d "$POSTGRES_DB" --clean --if-exists < backup_YYYYMMDD_HHMMSS.dump
```

> `$POSTGRES_USER` / `$POSTGRES_DB` は `.env.selfhost` の値に読み替えてください。

## 7. アップグレード

```bash
# 1) 最新コードを取得
git pull

# 2) バックアップ（上記 6 を推奨）

# 3) 再ビルドして再起動（api 起動時に新しいマイグレーションが自動適用される）
docker compose --env-file .env.selfhost -f docker-compose.selfhost.yml up -d --build

# 4) 後片付け（不要になった古いイメージ）
docker image prune -f
```

マイグレーションは EF Core により順方向にのみ適用されます。重大な変更の前はバックアップを取得してください。

## 8. 停止・削除

```bash
# 停止（データは保持）
docker compose --env-file .env.selfhost -f docker-compose.selfhost.yml down

# データごと削除（db_data ボリュームも削除。元に戻せません）
docker compose --env-file .env.selfhost -f docker-compose.selfhost.yml down -v
```

## 9. トラブルシューティング

- **ログインしても戻される**: HTTP で直接アクセスしている可能性があります。HTTPS（リバースプロキシ）経由でアクセスしてください（`Secure` Cookie）。
- **api が unhealthy**: `docker compose ... logs api` を確認。`JWT_KEY` が32文字未満だと起動時に失敗します。
- **403（アクセスが拒否されました）**: `ORIGIN_VERIFY_SECRET` が api と web で一致しているか確認してください。
