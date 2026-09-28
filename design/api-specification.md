# 案件・タスク管理システム API詳細仕様書

## 1. 文書情報

| 項目     | 内容                        |
| ------ | ------------------------- |
| 文書名    | 案件・タスク管理システム API詳細仕様書     |
| バージョン  | 1.6                       |
| 作成日    | 2026-09-22                |
| 更新日    | 2026-09-28                |
| 対象システム | 案件・タスク管理システム              |
| 上位文書   | 要件定義書 / 基本設計書 / ER図 / DDL |
| API方式  | REST API                  |
| データ形式  | JSON                      |
| 認証方式   | JWT                       |

## 改訂履歴

| バージョン | 日付 | 内容 |
| --- | --- | --- |
| 1.0 | 2026-09-22 | 初版作成 |
| 1.1 | 2026-09-28 | 認可チェックの実装に伴い、6.2 認可を追加。プロジェクト一覧の返却範囲、OWNER限定APIの403、担当者の所属チェックの400を追記。28 権限チェックを確定した内容に更新 |
| 1.2 | 2026-09-28 | ログイン保護の実装に伴い、5 共通HTTPステータスに429を追加し、8.1 ログインに試行回数の制限を追記 |
| 1.3 | 2026-09-28 | リフレッシュトークンの実装に伴い、8.1 ログインのレスポンスに `accessTokenExpiresAt`・`refreshToken` を追加し、8.2 リフレッシュ、8.3 ログアウトを追加 |
| 1.4 | 2026-09-28 | BFFへの移行に伴い、2 API概要の構成図を更新し、6.3 BFF を追加。APIのCORS設定の廃止を追記 |
| 1.5 | 2026-09-28 | 31 APIセキュリティにセキュリティヘッダーを追記 |
| 1.6 | 2026-09-28 | 6.1 にアクセストークンの有効期限の許容時間を追記。17 メンバー削除に最後のOWNERの409と担当タスクの扱いを追記。9.1 ユーザー一覧の公開範囲を追記 |

---

# 2. API概要

本システムでは、Next.jsで構築したフロントエンドとASP.NET Core Web APIの間でREST APIによる通信を行う。

基本構成は以下とする。

```text
Browser
   │
   │ HTTPS（HttpOnly のセッションCookie）
   ▼
Next.js / TypeScript（画面 ＋ BFF）
   │
   │ HTTPS / REST API（Authorization: Bearer {JWT}）
   ▼
ASP.NET Core Web API
   │
   ▼
Entity Framework Core / LINQ
   │
   ▼
PostgreSQL
```

---

# 3. API共通仕様

## 3.1 ベースURL

ローカル環境：

```text
http://localhost:5000/api
```

AWS環境ではAPI Gatewayのエンドポイントを使用する。

例：

```text
https://{api-domain}/api
```

実際のAWS URLはデプロイ時に確定する。

---

# 4. HTTPメソッド

| メソッド   | 用途    |
| ------ | ----- |
| GET    | データ取得 |
| POST   | データ作成 |
| PUT    | データ更新 |
| DELETE | データ削除 |

---

# 5. 共通HTTPステータス

| ステータス                     | 内容              |
| ------------------------- | --------------- |
| 200 OK                    | 正常終了            |
| 201 Created               | リソース作成成功        |
| 204 No Content            | 正常終了・レスポンスボディなし |
| 400 Bad Request           | リクエスト不正         |
| 401 Unauthorized          | 未認証             |
| 403 Forbidden             | 権限不足            |
| 404 Not Found             | 対象データなし         |
| 409 Conflict              | データ競合           |
| 429 Too Many Requests     | 試行回数の上限超過       |
| 500 Internal Server Error | サーバー内部エラー       |

---

# 6. 認証

## 6.1 認証が必要なAPI

ログインAPIを除き、原則としてAPIへのアクセスにはJWTによる認証を必要とする。

HTTPヘッダー：

```http
Authorization: Bearer {JWT}
```

アクセストークンの有効期限は、サーバー間の時刻のずれとして30秒まで許容する（`ClockSkew`）。有効期限から30秒を過ぎたトークンは401となる。

## 6.2 認可

プロジェクト配下のリソース（プロジェクト、メンバー、タスク、コメント）へのアクセスは、ログインユーザーがそのプロジェクトに所属しているかどうかと、プロジェクト内権限（OWNER / MEMBER）によって判定する。各操作の可否は 28章を参照。

| 状況 | HTTPステータス | レスポンス |
| --- | --- | --- |
| プロジェクトに所属していない | 404 | 対象が存在しない場合と同じ（`指定されたプロジェクトが存在しません。` / `指定されたタスクが存在しません。`） |
| 所属しているが、OWNER権限が必要な操作をMEMBERが行った | 403 | `{"message": "この操作を行う権限がありません。"}` |

所属していないプロジェクトについては、リソースの存在自体を開示しないため、403ではなく404を返す。

## 6.3 BFF

ブラウザは本APIを直接呼び出さず、Next.js の Route Handler（BFF）を経由する（`security-review.md` 5.3）。アクセストークンとリフレッシュトークンは BFF が暗号化Cookie（`tms_session`、HttpOnly / Secure / SameSite=Lax）に保存し、ブラウザの JavaScript には渡さない。

本APIはブラウザから直接呼び出されないため、CORS を設定しない（他のオリジンからのブラウザ経由の呼び出しは許可しない）。

| BFFのエンドポイント              | メソッド                 | 内容                                                                 |
| ------------------------ | -------------------- | ------------------------------------------------------------------ |
| `/api/bff/auth/login`    | POST                 | 8.1 ログインを呼び出し、トークンをセッションCookieに保存する。レスポンスは `{"user": {...}}` のみ（トークンは返さない） |
| `/api/bff/auth/logout`   | POST                 | 8.3 ログアウトを呼び出し、セッションCookieを削除する（204）                              |
| `/api/bff/auth/session`  | GET                  | ログイン中のユーザー情報を返す。ログインしていない、または再発行できない場合は401                         |
| `/api/bff/{APIのパス}`      | GET / POST / PUT / DELETE | `/api/{APIのパス}` にアクセストークンを付けて中継し、APIのレスポンスをそのまま返す。`auth` 配下は中継しない（404） |

**アクセストークンの再発行**

* BFF は、アクセストークンの有効期限の10秒前から、8.2 リフレッシュで再発行してセッションCookieを更新する。
* 再発行できない場合（リフレッシュトークンの期限切れ・失効・再利用検知）は、セッションを破棄して401を返す。画面は401を受けるとログイン画面に遷移する。

**CSRF対策**

更新系リクエスト（POST / PUT / DELETE / PATCH）では、以下をすべて満たさない場合に403を返す。

* `Origin` ヘッダーのホストが、リクエスト先（自サイト）のホストと一致する
* `X-Requested-With: XMLHttpRequest` ヘッダーが付いている

```json
{
  "message": "不正なリクエストです。"
}
```

---

# 7. 共通エラーレスポンス

APIでエラーが発生した場合、以下の形式を基本とする。

```json
{
  "message": "指定されたプロジェクトが存在しません。"
}
```

入力値エラーなど複数のエラーを返す場合：

```json
{
  "message": "入力内容に誤りがあります。",
  "errors": [
    {
      "field": "name",
      "message": "プロジェクト名は必須です。"
    }
  ]
}
```

---

# 8. 認証API

## 8.1 ログイン

### Endpoint

```http
POST /api/auth/login
```

### 認証

不要

### Request

```json
{
  "email": "user@example.com",
  "password": "password"
}
```

### Request項目

| 項目       | 型      | 必須 | 説明      |
| -------- | ------ | -- | ------- |
| email    | string | ○  | メールアドレス |
| password | string | ○  | パスワード   |

### Validation

* email必須
* password必須
* email形式チェック
* 登録済みユーザーか確認
* パスワード照合

### 試行回数の制限

同一メールアドレスでのログイン失敗が、最初の失敗から60秒以内に5回に達した場合、その60秒が経過するまで、パスワードの正誤に関わらずログインを受け付けない（429）。

* 失敗回数はメールアドレス単位で数える。大文字・小文字と前後の空白は区別しない
* 登録されていないメールアドレスも同じように数える（制限の有無から登録状況を推測できないようにする）
* ログインに成功すると失敗回数はリセットされる
* 上限回数と期間は `appsettings.json` の `LoginProtection`（`MaxFailedAttempts` / `FailureWindowSeconds`）で設定する
* IPアドレス単位の制限は、本番環境の API Gateway / AWS WAF で行う（`security-review.md` 5.2）

登録されていないメールアドレスの場合もパスワードの照合処理を行い、応答時間から登録状況を推測できないようにする。

### Response

HTTP 200

```json
{
  "accessToken": "eyJhbGciOiJIUzI1NiIs...",
  "accessTokenExpiresAt": "2026-09-28T12:15:00Z",
  "refreshToken": "Q2hhbmdlTWUtVGhpcy1Jcy1BLVNhbXBsZS1Ub2tlbg",
  "user": {
    "id": 1,
    "name": "山田太郎",
    "email": "user@example.com"
  }
}
```

| 項目                   | 型        | 説明                                                  |
| -------------------- | -------- | --------------------------------------------------- |
| accessToken          | string   | APIの呼び出しに使うJWT。有効期限は15分                              |
| accessTokenExpiresAt | datetime | アクセストークンの有効期限（UTC）。BFFが再発行のタイミングを判断するために使う              |
| refreshToken         | string   | アクセストークンの再発行に使うトークン。有効期限は7日。APIの呼び出しには使えない               |
| user                 | object   | ログインユーザーの情報                                          |

### エラー

#### 認証失敗

HTTP 401

```json
{
  "message": "メールアドレスまたはパスワードが正しくありません。"
}
```

#### 試行回数の上限に達している場合

HTTP 429

`Retry-After` ヘッダーに、再試行できるまでの秒数を返す。

```http
Retry-After: 42
```

```json
{
  "message": "ログインの試行回数が上限に達しました。しばらくしてから再度お試しください。"
}
```

---

## 8.2 リフレッシュ

アクセストークンを再発行する。使用したリフレッシュトークンは失効し、新しいリフレッシュトークンが発行される（ローテーション）。

### Endpoint

```http
POST /api/auth/refresh
```

### 認証

不要（リクエストのリフレッシュトークンで認証する）

### Request

```json
{
  "refreshToken": "Q2hhbmdlTWUtVGhpcy1Jcy1BLVNhbXBsZS1Ub2tlbg"
}
```

### Validation

* refreshToken必須

### Response

HTTP 200

ログインAPIと同じ形式（`accessToken`、`accessTokenExpiresAt`、新しい`refreshToken`、`user`）。

### 置き換え済みトークンの再利用

* 置き換え済みのリフレッシュトークンが、置き換えから10秒以内に再び使われた場合は、画面からの同時リクエストによる正常な利用とみなし、新しいトークンを発行する。
* 10秒を過ぎてから使われた場合は、トークンが漏洩したとみなし、そのユーザーの有効なリフレッシュトークンをすべて失効させて401を返す（再利用検知）。
* 猶予期間は `appsettings.json` の `RefreshToken:ReuseGraceSeconds` で設定する。

### エラー

#### リフレッシュトークンが無効な場合（存在しない、有効期限切れ、失効済み、再利用検知）

HTTP 401

```json
{
  "message": "認証の有効期限が切れました。再度ログインしてください。"
}
```

---

## 8.3 ログアウト

リフレッシュトークンを失効させる。

### Endpoint

```http
POST /api/auth/logout
```

### 認証

不要（リクエストのリフレッシュトークンで対象を特定する）

### Request

```json
{
  "refreshToken": "Q2hhbmdlTWUtVGhpcy1Jcy1BLVNhbXBsZS1Ub2tlbg"
}
```

### Response

HTTP 204

レスポンスボディなし。存在しないトークン、失効済みのトークンが指定された場合も204を返す。

アクセストークン（JWT）はサーバー側で失効させないため、有効期限（15分）までは有効なままとなる。

---

# 9. ユーザーAPI

## 9.1 ユーザー一覧取得

### Endpoint

```http
GET /api/users
```

### 認証

必要

### Response

HTTP 200

```json
[
  {
    "id": 1,
    "name": "山田太郎",
    "email": "yamada@example.com"
  },
  {
    "id": 2,
    "name": "佐藤花子",
    "email": "sato@example.com"
  }
]
```

### DB

```text
users
```

### 公開範囲

ログイン済みのすべてのユーザーが、全ユーザーの氏名・メールアドレスを取得できる。組織内で利用するシステムとして、利用者どうしが一覧を参照できることを許容している（`security-review.md` 5.6 SEC-09）。

---

## 9.2 ユーザー詳細取得

### Endpoint

```http
GET /api/users/{id}
```

### Path Parameter

| 項目 | 型      | 必須 | 説明     |
| -- | ------ | -- | ------ |
| id | bigint | ○  | ユーザーID |

### Response

HTTP 200

```json
{
  "id": 1,
  "name": "山田太郎",
  "email": "yamada@example.com"
}
```

### 対象ユーザーが存在しない場合

HTTP 404

```json
{
  "message": "指定されたユーザーが存在しません。"
}
```

---

# 10. プロジェクトAPI

## 10.1 プロジェクト一覧取得

### Endpoint

```http
GET /api/projects
```

### 認証

必要

### 取得範囲

ログインユーザーが所属しているプロジェクト（OWNER / MEMBER）のみを返す。

### Response

HTTP 200

```json
[
  {
    "id": 1,
    "name": "案件管理システム",
    "description": "案件・タスク管理用プロジェクト",
    "status": "ACTIVE",
    "startDate": "2026-09-01",
    "endDate": null
  }
]
```

### DB

```text
projects
```

---

# 11. プロジェクト登録

### Endpoint

```http
POST /api/projects
```

### 認証

必要

### Request

```json
{
  "name": "案件管理システム",
  "description": "案件・タスク管理用プロジェクト",
  "status": "ACTIVE",
  "startDate": "2026-09-01",
  "endDate": null
}
```

### Request項目

| 項目          | 型      | 必須 | 説明       |
| ----------- | ------ | -- | -------- |
| name        | string | ○  | プロジェクト名  |
| description | string | -  | 説明       |
| status      | string | -  | プロジェクト状態 |
| startDate   | date   | -  | 開始日      |
| endDate     | date   | -  | 終了日      |

### status

```text
ACTIVE
COMPLETED
ARCHIVED
```

### Validation

* name必須
* name最大200文字
* statusが許可された値であること
* startDate / endDateの日付形式チェック

### Response

HTTP 201

```json
{
  "id": 1,
  "name": "案件管理システム",
  "description": "案件・タスク管理用プロジェクト",
  "status": "ACTIVE",
  "startDate": "2026-09-01",
  "endDate": null
}
```

### DB

```text
projects
```

---

# 12. プロジェクト詳細取得

### Endpoint

```http
GET /api/projects/{id}
```

### 認証

必要

### Response

HTTP 200

```json
{
  "id": 1,
  "name": "案件管理システム",
  "description": "案件・タスク管理用プロジェクト",
  "status": "ACTIVE",
  "startDate": "2026-09-01",
  "endDate": null
}
```

### エラー

HTTP 404

```json
{
  "message": "指定されたプロジェクトが存在しません。"
}
```

---

# 13. プロジェクト更新

### Endpoint

```http
PUT /api/projects/{id}
```

### 認証

必要

### 権限

OWNERのみ（6.2 参照）

### Request

```json
{
  "name": "案件・タスク管理システム",
  "description": "プロジェクト管理機能を含むWebアプリケーション",
  "status": "ACTIVE",
  "startDate": "2026-09-01",
  "endDate": null
}
```

### Response

HTTP 200

```json
{
  "id": 1,
  "name": "案件・タスク管理システム",
  "description": "プロジェクト管理機能を含むWebアプリケーション",
  "status": "ACTIVE",
  "startDate": "2026-09-01",
  "endDate": null
}
```

### エラー

#### 指定されたプロジェクトが存在しない場合（所属していない場合を含む）

HTTP 404

```json
{
  "message": "指定されたプロジェクトが存在しません。"
}
```

#### 権限がない場合（MEMBERによる操作）

HTTP 403

```json
{
  "message": "この操作を行う権限がありません。"
}
```

---

# 14. プロジェクト削除

### Endpoint

```http
DELETE /api/projects/{id}
```

### 認証

必要

### 権限

OWNERのみ（6.2 参照）

### Response

HTTP 204

レスポンスボディなし。

### エラー

#### 指定されたプロジェクトが存在しない場合（所属していない場合を含む）

HTTP 404

```json
{
  "message": "指定されたプロジェクトが存在しません。"
}
```

#### 権限がない場合（MEMBERによる操作）

HTTP 403

```json
{
  "message": "この操作を行う権限がありません。"
}
```

### 削除時のDB動作

プロジェクトを削除すると、外部キー制約により以下も削除される。

```text
projects
  ↓
project_members
  ↓
tasks
  ↓
task_comments
task_status_histories
```

---

# 15. プロジェクトメンバーAPI

## 15.1 メンバー一覧取得

### Endpoint

```http
GET /api/projects/{projectId}/members
```

### 認証

必要

### Response

HTTP 200

```json
[
  {
    "userId": 1,
    "name": "山田太郎",
    "email": "yamada@example.com",
    "role": "OWNER"
  },
  {
    "userId": 2,
    "name": "佐藤花子",
    "email": "sato@example.com",
    "role": "MEMBER"
  }
]
```

### DB

```text
project_members
users
```

### エラー

指定されたプロジェクトが存在しない場合

HTTP 404

```json
{
  "message": "指定されたプロジェクトが存在しません。"
}
```

---

# 16. プロジェクトメンバー追加

### Endpoint

```http
POST /api/projects/{projectId}/members
```

### 認証

必要

### 権限

OWNERのみ（6.2 参照）

### Request

```json
{
  "userId": 2,
  "role": "MEMBER"
}
```

### Request項目

| 項目     | 型      | 必須 | 説明        |
| ------ | ------ | -- | --------- |
| userId | bigint | ○  | ユーザーID    |
| role   | string | ○  | プロジェクト内権限 |

### role

```text
OWNER
MEMBER
```

### Validation

* userId必須
* 指定ユーザーが存在すること
* 指定プロジェクトが存在すること
* 同一ユーザーが既に所属していないこと
* roleが許可された値であること

### Response

HTTP 201

```json
{
  "projectId": 1,
  "userId": 2,
  "role": "MEMBER"
}
```

### エラー

#### 指定されたプロジェクトが存在しない場合（所属していない場合を含む）

HTTP 404

```json
{
  "message": "指定されたプロジェクトが存在しません。"
}
```

#### 権限がない場合（MEMBERによる操作）

HTTP 403

```json
{
  "message": "この操作を行う権限がありません。"
}
```

#### 指定されたユーザーが存在しない場合

HTTP 400

```json
{
  "message": "入力内容に誤りがあります。",
  "errors": [
    {
      "field": "userId",
      "message": "指定されたユーザーが存在しません。"
    }
  ]
}
```

#### 同一ユーザーが既にプロジェクトに参加している場合

データ競合のため HTTP 409 を返す。

```json
{
  "message": "指定されたユーザーは既にプロジェクトに参加しています。"
}
```

---

# 17. プロジェクトメンバー削除

### Endpoint

```http
DELETE /api/projects/{projectId}/members/{userId}
```

### 認証

必要

### 権限

OWNERのみ（6.2 参照）

### Response

HTTP 204

レスポンスボディなし。

### エラー

#### 指定されたプロジェクトが存在しない場合（所属していない場合を含む）

HTTP 404

```json
{
  "message": "指定されたプロジェクトが存在しません。"
}
```

#### 権限がない場合（MEMBERによる操作）

HTTP 403

```json
{
  "message": "この操作を行う権限がありません。"
}
```

#### 指定されたメンバーが存在しない場合

HTTP 404

```json
{
  "message": "指定されたメンバーが存在しません。"
}
```

#### プロジェクトの最後のOWNERを削除しようとした場合

OWNERが1人もいなくなると、プロジェクトを管理できるユーザーがいなくなるため、削除できない。

HTTP 409

```json
{
  "message": "プロジェクトには少なくとも1人のOWNERが必要です。"
}
```

### 担当タスクの扱い

削除したユーザーが、そのプロジェクトで担当者になっているタスクは、未割り当て（`assigneeId` が `null`）にする。担当者はプロジェクトのメンバーに限るため（`security-review.md` SEC-10 / SEC-13）。他のプロジェクトのタスクは変更しない。

---

# 18. タスクAPI

## 18.1 プロジェクト内タスク一覧

### Endpoint

```http
GET /api/projects/{projectId}/tasks
```

### 認証

必要

### Response

HTTP 200

```json
[
  {
    "id": 1,
    "projectId": 1,
    "assigneeId": 2,
    "title": "API設計",
    "description": "REST APIの仕様を作成する",
    "status": "IN_PROGRESS",
    "priority": "HIGH",
    "dueDate": "2026-09-30"
  }
]
```

### DB

```text
tasks
```

### エラー

指定されたプロジェクトが存在しない場合

HTTP 404

```json
{
  "message": "指定されたプロジェクトが存在しません。"
}
```

---

# 19. タスク登録

### Endpoint

```http
POST /api/projects/{projectId}/tasks
```

### 認証

必要

### Request

```json
{
  "assigneeId": 2,
  "title": "API設計",
  "description": "REST APIの仕様を作成する",
  "status": "TODO",
  "priority": "HIGH",
  "dueDate": "2026-09-30"
}
```

### Request項目

| 項目          | 型      | 必須 | 説明       |
| ----------- | ------ | -- | -------- |
| assigneeId  | bigint | -  | 担当ユーザーID |
| title       | string | ○  | タスク名     |
| description | string | -  | タスク説明    |
| status      | string | -  | タスク状態    |
| priority    | string | -  | 優先度      |
| dueDate     | date   | -  | 期限       |

### status

```text
TODO
IN_PROGRESS
DONE
```

### priority

```text
LOW
MEDIUM
HIGH
```

### Validation

* title必須
* title最大200文字
* projectIdが存在すること
* assigneeIdを指定した場合、ユーザーが存在し、プロジェクトのメンバーであること
* statusが許可された値であること
* priorityが許可された値であること

### Response

HTTP 201

```json
{
  "id": 1,
  "projectId": 1,
  "assigneeId": 2,
  "title": "API設計",
  "description": "REST APIの仕様を作成する",
  "status": "TODO",
  "priority": "HIGH",
  "dueDate": "2026-09-30"
}
```

### エラー

#### 担当者にプロジェクトのメンバー以外を指定した場合

HTTP 400

```json
{
  "message": "入力内容に誤りがあります。",
  "errors": [
    {
      "field": "assigneeId",
      "message": "指定されたユーザーはプロジェクトのメンバーではありません。"
    }
  ]
}
```

---

# 20. タスク詳細取得

### Endpoint

```http
GET /api/tasks/{id}
```

### 認証

必要

### Response

HTTP 200

```json
{
  "id": 1,
  "projectId": 1,
  "assigneeId": 2,
  "title": "API設計",
  "description": "REST APIの仕様を作成する",
  "status": "IN_PROGRESS",
  "priority": "HIGH",
  "dueDate": "2026-09-30"
}
```

### エラー

HTTP 404

```json
{
  "message": "指定されたタスクが存在しません。"
}
```

---

# 21. タスク更新

### Endpoint

```http
PUT /api/tasks/{id}
```

### 認証

必要

### Request

```json
{
  "assigneeId": 2,
  "title": "API詳細設計",
  "description": "REST APIの詳細仕様を作成する",
  "status": "IN_PROGRESS",
  "priority": "HIGH",
  "dueDate": "2026-09-30"
}
```

### Response

HTTP 200

```json
{
  "id": 1,
  "projectId": 1,
  "assigneeId": 2,
  "title": "API詳細設計",
  "description": "REST APIの詳細仕様を作成する",
  "status": "IN_PROGRESS",
  "priority": "HIGH",
  "dueDate": "2026-09-30"
}
```

### エラー

#### 担当者にプロジェクトのメンバー以外を指定した場合

HTTP 400

```json
{
  "message": "入力内容に誤りがあります。",
  "errors": [
    {
      "field": "assigneeId",
      "message": "指定されたユーザーはプロジェクトのメンバーではありません。"
    }
  ]
}
```

---

# 22. タスク削除

### Endpoint

```http
DELETE /api/tasks/{id}
```

### 認証

必要

### 権限

OWNERのみ（6.2 参照）

### Response

HTTP 204

レスポンスボディなし。

### エラー

#### 指定されたタスクが存在しない場合（所属していない場合を含む）

HTTP 404

```json
{
  "message": "指定されたタスクが存在しません。"
}
```

#### 権限がない場合（MEMBERによる操作）

HTTP 403

```json
{
  "message": "この操作を行う権限がありません。"
}
```

### 削除時のDB動作

タスク削除時には、外部キー制約により関連する以下のデータも削除される。

```text
tasks
  ↓
task_comments
task_status_histories
```

---

# 23. コメントAPI

## 23.1 コメント一覧取得

### Endpoint

```http
GET /api/tasks/{taskId}/comments
```

### 認証

必要

### Response

HTTP 200

```json
[
  {
    "id": 1,
    "taskId": 1,
    "userId": 2,
    "userName": "佐藤花子",
    "comment": "API仕様を確認しました。",
    "createdAt": "2026-09-22T10:00:00"
  }
]
```

### DB

```text
task_comments
users
```

### エラー

指定されたタスクが存在しない場合

HTTP 404

```json
{
  "message": "指定されたタスクが存在しません。"
}
```

---

# 24. コメント登録

### Endpoint

```http
POST /api/tasks/{taskId}/comments
```

### 認証

必要

### Request

```json
{
  "comment": "API仕様を確認しました。"
}
```

### Request項目

| 項目      | 型      | 必須 | 説明     |
| ------- | ------ | -- | ------ |
| comment | string | ○  | コメント本文 |

### Validation

* comment必須
* 対象タスクが存在すること
* コメント本文は1000文字以内

### Response

HTTP 201

```json
{
  "id": 1,
  "taskId": 1,
  "userId": 2,
  "comment": "API仕様を確認しました。",
  "createdAt": "2026-09-22T10:00:00"
}
```

### エラー

指定されたタスクが存在しない場合

HTTP 404

```json
{
  "message": "指定されたタスクが存在しません。"
}
```

---

# 25. Phase 2 API

MVP完成後、以下のAPIを追加する。

## 25.1 タスク検索・絞り込み

```http
GET /api/tasks
```

想定検索条件：

```text
projectId
assigneeId
status
priority
dueDate
keyword
```

例：

```http
GET /api/tasks?projectId=1&status=IN_PROGRESS
```

---

## 25.2 タスクステータス履歴

### 履歴取得

```http
GET /api/tasks/{taskId}/status-histories
```

### Response

```json
[
  {
    "id": 1,
    "taskId": 1,
    "changedBy": 2,
    "fromStatus": "TODO",
    "toStatus": "IN_PROGRESS",
    "createdAt": "2026-09-22T10:00:00"
  }
]
```

---

## 25.3 ダッシュボード

```http
GET /api/dashboard
```

ダッシュボード専用テーブルは作成せず、以下のデータを集計する。

```text
projects
tasks
users
```

想定レスポンス：

```json
{
  "projectCount": 5,
  "taskCount": 30,
  "todoCount": 10,
  "inProgressCount": 12,
  "doneCount": 8
}
```

---

# 26. APIとDBの対応

| API機能    | 主なテーブル                   |
| -------- | ------------------------ |
| ログイン     | users                    |
| ユーザー一覧   | users                    |
| ユーザー詳細   | users                    |
| プロジェクト一覧 | projects                 |
| プロジェクト登録 | projects                 |
| プロジェクト詳細 | projects                 |
| プロジェクト更新 | projects                 |
| プロジェクト削除 | projects                 |
| メンバー一覧   | project_members / users  |
| メンバー追加   | project_members / users  |
| メンバー削除   | project_members          |
| タスク一覧    | tasks                    |
| タスク登録    | tasks                    |
| タスク詳細    | tasks                    |
| タスク更新    | tasks                    |
| タスク削除    | tasks                    |
| コメント一覧   | task_comments / users    |
| コメント登録   | task_comments            |
| ステータス履歴  | task_status_histories    |
| ダッシュボード  | projects / tasks / users |
| タスク検索    | tasks                    |

---

# 27. APIと画面の対応

| 画面       | 主なAPI                                  |
| -------- | -------------------------------------- |
| ログイン     | POST `/api/auth/login`                 |
| プロジェクト一覧 | GET `/api/projects`                    |
| プロジェクト登録 | POST `/api/projects`                   |
| プロジェクト詳細 | GET `/api/projects/{id}`               |
| プロジェクト編集 | PUT `/api/projects/{id}`               |
| メンバー管理   | `/api/projects/{projectId}/members`    |
| タスク一覧    | GET `/api/projects/{projectId}/tasks`  |
| タスク登録    | POST `/api/projects/{projectId}/tasks` |
| タスク詳細    | GET `/api/tasks/{id}`                  |
| タスク編集    | PUT `/api/tasks/{id}`                  |
| タスク削除    | DELETE `/api/tasks/{id}`               |
| コメント一覧   | GET `/api/tasks/{taskId}/comments`     |
| コメント登録   | POST `/api/tasks/{taskId}/comments`    |
| ダッシュボード  | GET `/api/dashboard` ※Phase 2          |

---

# 28. 権限チェック

APIでは、認証に加えて、プロジェクトへの所属とプロジェクト内権限による認可チェックを行う（6.2 参照）。

| 処理           | OWNER | MEMBER | 非メンバー |
| ------------ | :---: | :----: | :---: |
| プロジェクト一覧     | 所属分のみ | 所属分のみ  | －     |
| プロジェクト作成     | ○     | ○      | ○     |
| プロジェクト参照     | ○     | ○      | 404   |
| プロジェクト更新     | ○     | 403    | 404   |
| プロジェクト削除     | ○     | 403    | 404   |
| メンバー参照       | ○     | ○      | 404   |
| メンバー追加       | ○     | 403    | 404   |
| メンバー削除       | ○     | 403    | 404   |
| タスク参照        | ○     | ○      | 404   |
| タスク作成        | ○     | ○      | 404   |
| タスク更新        | ○     | ○      | 404   |
| タスク削除        | ○     | 403    | 404   |
| コメント参照・登録    | ○     | ○      | 404   |

* プロジェクト作成は認証済みの全ユーザーに許可し、作成者がOWNERになる。
* タスクの担当者は、そのプロジェクトのメンバーに限定する。
* タスク削除は、タスクに作成者の情報を持たないため OWNER のみに許可する。
* 要件定義書のシステムロール（管理者）は MVP では扱わない。詳細は `security-review.md` 5.1 を参照。

---

# 29. DBアクセス方針

バックエンドではEntity Framework Coreを使用する。

データ取得・更新には原則としてLINQを使用する。

例：

```csharp
var tasks = await dbContext.Tasks
    .Where(x => x.ProjectId == projectId)
    .OrderByDescending(x => x.CreatedAt)
    .ToListAsync();
```

APIから直接SQLを実行する方式は基本的に採用しない。

---

# 30. トランザクション

複数のDB更新を一連の処理として扱う必要がある場合、トランザクションを使用する。

例：

```text
プロジェクト作成
    ↓
プロジェクトメンバー登録
```

また、Phase 2でステータス履歴を実装する場合、

```text
タスクステータス更新
        ↓
ステータス履歴登録
```

を同一トランザクションとして扱うことを検討する。

---

# 31. APIセキュリティ

以下の対策を実施する。

* JWTによる認証
* APIごとの認可チェック
* HTTPS通信
* 入力値バリデーション
* SQLインジェクション対策
* Entity Framework Coreによるパラメータ化
* パスワードのハッシュ化
* JWTやパスワード等の機密情報をログへ出力しない
* 秘密情報をGitHubへ登録しない
* 環境変数による設定値管理
* すべてのレスポンスにセキュリティヘッダーを付与する（`X-Content-Type-Options: nosniff`、`X-Frame-Options: DENY`、`Content-Security-Policy: default-src 'none'; frame-ancestors 'none'`）。`Server` ヘッダーは出力しない（`security-review.md` 5.5）

---

# 32. API設計上の留意事項

## 32.1 REST API

リソース単位でURLを設計する。

例：

```text
/projects
/projects/{id}
/projects/{projectId}/tasks
/tasks/{id}
/tasks/{taskId}/comments
```

## 32.2 JSON

リクエストおよびレスポンスはJSONを基本とする。

## 32.3 エラーレスポンス

利用者が原因を把握できる情報を返す。

ただし、内部システム情報や機密情報は返却しない。

## 32.4 ページング

MVPでは必須とせず、Phase 2の検索・絞り込み機能実装時に導入を検討する。

---

# 33. MVP API一覧

MVPでは以下のAPIを実装対象とする。

### 認証

```text
POST /api/auth/login
```

### ユーザー

```text
GET /api/users
GET /api/users/{id}
```

### プロジェクト

```text
GET    /api/projects
POST   /api/projects
GET    /api/projects/{id}
PUT    /api/projects/{id}
DELETE /api/projects/{id}
```

### メンバー

```text
GET    /api/projects/{projectId}/members
POST   /api/projects/{projectId}/members
DELETE /api/projects/{projectId}/members/{userId}
```

### タスク

```text
GET    /api/projects/{projectId}/tasks
POST   /api/projects/{projectId}/tasks
GET    /api/tasks/{id}
PUT    /api/tasks/{id}
DELETE /api/tasks/{id}
```

### コメント

```text
GET  /api/tasks/{taskId}/comments
POST /api/tasks/{taskId}/comments
```

---

# 34. API詳細設計完了条件

以下を満たした時点でAPI詳細設計を完了とする。

* [ ] MVP対象APIが定義されている
* [ ] HTTPメソッドが定義されている
* [ ] URLが定義されている
* [ ] 認証要否が定義されている
* [ ] リクエスト項目が定義されている
* [ ] バリデーション方針が定義されている
* [ ] 正常レスポンスが定義されている
* [ ] エラーレスポンスが定義されている
* [ ] HTTPステータスコードが定義されている
* [ ] DBアクセス対象が定義されている
* [ ] 権限チェック方針が定義されている
* [ ] Phase 2 APIが整理されている
* [ ] ER図・DDLとの整合性が確認されている

---

# 35. 次工程

API詳細設計完了後、画面詳細設計を行う。

作成対象：

```text
docs/design/screen-design.md
```

画面詳細設計では、以下を定義する。

* 画面レイアウト
* 入力項目
* 表示項目
* ボタン
* バリデーション
* API連携
* 画面遷移
* エラー表示
* 認証状態
