# infra/terraform

AWS 環境（Lambda・API Gateway・RDS・Amplify 等）を Terraform で管理します。

各リソースの設定値と、その設計判断（セキュリティ・コスト）は [AWS構成設計書](../../design/aws-architecture.md) を参照してください。このREADMEには Terraform の使い方のみを記載します。

## 方針

| 項目 | 方針 |
| --- | --- |
| リージョン | 東京（`ap-northeast-1`） |
| state の保存先 | ローカル（`terraform.tfstate`）。Git には含めない（`.gitignore` 済み） |
| 秘密情報 | **Terraform・state・リポジトリに置かない**。RDS のパスワードは RDS が Secrets Manager で管理し（`manage_master_user_password`）、`Jwt:Key`・`SESSION_SECRET` 等は Terraform で作った Secrets Manager の入れ物に手動で登録する |
| タグ | すべてのリソースに `Project` / `Environment` / `ManagedBy` を付ける（`providers.tf` の `default_tags`） |
| DNS | Route 53 は使わない。Amplify のカスタムドメイン（`tms.accent24.jp`）用の CNAME を、ムームーDNS に手動で追加する |

state には秘密情報を含めない構成にしていますが、作成したリソースの情報が含まれるため、PC の故障に備えて別の場所にバックアップしてください。GitHub Actions や複数の PC から実行する必要が出たら、S3 backend に移行します（`terraform init -migrate-state`）。

## 準備（初回のみ）

1. Terraform（1.9 以上）と AWS CLI をインストールする
2. ルートユーザーでサインインして、一時的な認証情報を取得する（ルートユーザーのアクセスキーは作成しない。[AWS構成設計書 4.12](../../design/aws-architecture.md)）

   ```bash
   aws login --profile tms-login
   ```

   `~/.aws/config` に、Terraform 用のプロファイル `tms` を追加する。Terraform の AWS プロバイダーは `aws login` の認証情報を直接読み込めないため、AWS CLI を経由して渡す。

   ```ini
   [profile tms]
   region = ap-northeast-1
   credential_process = aws configure export-credentials --profile tms-login --format process
   ```

   一時的な認証情報は最長12時間で失効するため、失効したら `aws login --profile tms-login` を再度実行する。

3. 変数ファイルを作成する

   ```bash
   cp terraform.tfvars.example terraform.tfvars
   ```

## 実行

```bash
terraform init
```

```bash
terraform plan
```

`plan` の結果に表示される `aws_account_id` が、意図したアカウントであることを確認してから実行します。

```bash
terraform apply
```

初回の `terraform init` で作成される `.terraform.lock.hcl`（プロバイダーのバージョンの固定）はコミットします。

## apply 後の手動手順（AWS 仕様で Terraform では完結できないもの）

`apply` だけでは完結しない作業がいくつかあります。これは設計上の割り切りです。

1. **Secrets の値登録**：`Jwt:Key`・`SESSION_SECRET` 等を、作成済みの Secrets Manager の入れ物に手動登録する。
2. **DNS（ムームーDNS）へのレコード登録**：`terraform output` に出る Amplify 用 CNAME／証明書検証レコードを登録する。
3. **DB マイグレーション**：マイグレーション用 Lambda を実行して反映する。
4. **SNS アラート購読の確認**：届く確認メールのリンクをクリックする。
5. **メール通知（SES / M3 step6）**：
   - `terraform output ses_dkim_cname_records` に出る **DKIM 用 CNAME（3件）をムームーDNS に登録**する → これで送信ドメイン（`tms.accent24.jp`）が検証済みになる。
   - **SES サンドボックス解除（本番送信枠）を AWS サポートへ申請**する（Terraform 不可）。解除までは検証済みアドレス宛のみ送信可。
   - 差出人は変数 `ses_from_address`（既定 `no-reply@tms.accent24.jp`）。API Lambda には `Email__FromAddress`・`App__BaseUrl` が渡り、`ses:SendEmail`（この identity 限定）が付与される。
   - 上記が未完でも、アプリは送信失敗をログに記録して処理を継続する（アプリ内通知は保存済み）。

## CI

プルリクエストごとに `terraform fmt -check` と `terraform validate` を実行します（AWS には接続しません。`.github/workflows/ci.yml`）。
