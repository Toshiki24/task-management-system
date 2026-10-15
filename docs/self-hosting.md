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

## 4. 初期管理者の作成（初回セットアップ画面）

自己ホストには利用者の自由登録はありません。最初の System Admin は、初回起動後に**ブラウザのセットアップ画面**から作成します（パスワードを環境変数で決め打ちしません）。

1. 起動後、ブラウザで `/`（または `/login`）を開くと、ユーザーが未作成のときは自動で **`/setup`** に誘導されます。
2. 管理者名・メールアドレス・パスワード（12文字以上）・パスワード確認を入力して作成します。
3. 作成後は通常のログイン画面に戻るので、その管理者でログインします。

**以降のユーザー追加は、管理者による招待フロー（M1）** で行います（ワークスペース設定からメール招待 → 被招待者がトークンで参加）。

> ⚠️ **初回公開前の注意**: セットアップが未完了の状態で外部公開すると、第三者が先に管理者を作成できてしまいます。
> 本 compose は既定で `web` を `127.0.0.1` のみに公開します。**初回管理者の作成を終えるまで外部に公開しない**でください
> （ローカルで作成を済ませてからリバースプロキシで公開する運用を推奨）。
> セットアップは「ユーザーが1人も居ないとき」だけ有効で、作成後は `/setup` を開いても何も作成できません（サーバー側で保証）。
> `SELF_HOST_SETUP` は compose 側で有効化済みです。AWS 本番ではこのフラグを設定しないため、セットアップ機能は無効になります。

## 5. ヘルスチェック・可観測性

- API: `GET /health` は**DB 接続とマイグレーション状態**を確認し、JSON で状態を返します（認証・オリジン検証なしで到達可能、秘密情報なし）。正常なら 200、異常なら 503。

  ```json
  { "status": "ok", "checks": { "database": { "status": "ok", "data": { "pendingMigrations": 0 } } } }
  ```

- Compose の healthcheck で `api`（curl で `/health`）と `web`（Node fetch で `/login`）の死活を監視します。

  ```bash
  docker compose --env-file .env.selfhost -f docker-compose.selfhost.yml exec api curl -fsS http://localhost:8080/health
  ```

- **構造化ログ**: 本番/自己ホスト（非 Development）では、API がリクエストごとに JSON ログを標準出力に出します（相関 ID `X-Request-Id`・HTTP メソッド・パス・ステータス・所要時間・ユーザー ID を含み、トークンや本文・クエリ文字列は出しません）。`docker compose ... logs api` で確認でき、CloudWatch 等ではこれらのフィールドから件数・エラー率・レイテンシを集計できます。相関 ID はレスポンスヘッダー `X-Request-Id` にも返り、クライアントが指定すれば引き継がれます。
- **運用の一次情報**: 重要操作は監査ログ（`audit_logs`、管理コンソールから閲覧）、タスクの動きは活動（`activities`）に記録されます。

## 6. メール送信（SMTP / 招待・通知メール）

招待メールや各種通知メールは、**`SMTP_HOST` を設定すると実送信**、未設定なら送信内容をログに出力するだけ（招待リンクはログで確認）になります。標準的な SMTP（25 / 587 STARTTLS + 認証）に対応し、Gmail・Amazon SES(SMTP)・SendGrid・Mailgun・Office365・さくら等、どのプロバイダでも使えます。

SMTP を設定すると、以下のタイミングで本人宛にメールが届きます（アプリ内通知は SMTP 設定に関わらず常に表示されます）:

- コメントでの **@メンション** / 自分が関わるタスクへの**コメント**
- タスクの**状態変更**（ウォッチ中のタスク）
- タスクの**担当者割り当て**
- タスクの**期限接近 / 超過**（期限通知ジョブの実行時）

各ユーザーは画面右上メニューの **「設定」→「通知設定」** でメール通知のオン/オフを切り替えられます（既定はオン）。

`.env.selfhost` の設定例:

```bash
# 共通
SMTP_HOST=smtp.gmail.com         # 例: Gmail。SES は email-smtp.<region>.amazonaws.com
SMTP_PORT=587                    # 587(STARTTLS)を推奨。465 の暗黙TLSのみのサーバーは未対応
SMTP_USER=you@example.com        # 認証ユーザー(任意。社内リレー等で不要なら空でも可)
SMTP_PASSWORD=＜アプリパスワード等＞
SMTP_FROM=noreply@example.com    # 送信元(未設定なら SMTP_USER)。多くのプロバイダは認証ドメインと一致を要求
SMTP_FROM_NAME=タスク管理システム
SMTP_USE_STARTTLS=true

# 招待メールのリンクを絶対URLにする(公開URL)
APP_BASE_URL=https://tasks.example.com
```

- **`APP_BASE_URL` を設定**すると、招待メール内のリンクが `https://＜公開URL＞/invitations/accept?token=...` の絶対URLになります（未設定だと相対パス）。
- 送信に失敗した場合は api のログにエラーが出ます（本文＝招待トークンはログに出しません）。
- SMTP 未設定のままでも、招待リンクは `docker compose ... logs api` で確認できます（§9 参照）。
- Gmail は「アプリ パスワード」、Amazon SES は「SMTP 認証情報」を使ってください（通常のログインパスワードや AWS のアクセスキーではありません）。

## 7. バックアップ / リストア

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

## 8. アップグレード

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

## 9. 停止・削除

```bash
# 停止（データは保持）
docker compose --env-file .env.selfhost -f docker-compose.selfhost.yml down

# データごと削除（db_data ボリュームも削除。元に戻せません）
docker compose --env-file .env.selfhost -f docker-compose.selfhost.yml down -v
```

## 10. トラブルシューティング

- **ログインしても戻される**: HTTP で直接アクセスしている可能性があります。HTTPS（リバースプロキシ）経由でアクセスしてください（`Secure` Cookie）。
- **api が unhealthy**: `docker compose ... logs api` を確認。`JWT_KEY` が32文字未満だと起動時に失敗します。
- **api 起動時に `28P01: password authentication failed for user "postgres"`**: PostgreSQL は**データディレクトリが空の初回起動時のみ** `POSTGRES_PASSWORD` を適用します。以前 `up` して作られたボリューム `db_data` が残っている状態で `.env.selfhost` のパスワードを変えても、**DB 側の実パスワードは以前のまま**のため認証に失敗します（api と db は同じ `${POSTGRES_PASSWORD}` を参照するので、食い違う原因はこの古いボリュームです）。まだ保持すべきデータが無ければ、ボリュームごと作り直して現在のパスワードで初期化します:

  ```bash
  docker compose --env-file .env.selfhost -f docker-compose.selfhost.yml down -v
  docker compose --env-file .env.selfhost -f docker-compose.selfhost.yml up -d --build
  ```

  既存データを残したままパスワードだけ変えたい場合は、`down -v` せずに DB 内で `ALTER USER postgres PASSWORD '...'` を実行し、`.env.selfhost` を同じ値に合わせてください。なお、パスワードに `;`・引用符・前後の空白を含めると接続文字列の解析で別の不具合になり得ます（`openssl rand -base64 36` 由来の `+ / =` は問題ありません）。
- **403（アクセスが拒否されました）**: `ORIGIN_VERIFY_SECRET` が api と web で一致しているか確認してください。
