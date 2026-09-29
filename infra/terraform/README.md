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
2. 作業用 IAM ユーザーのアクセスキーを、プロファイル `tms` として設定する（アクセスキーは自分で入力し、ファイルやチャットに貼らない）

   ```bash
   aws configure --profile tms
   ```

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

## CI

プルリクエストごとに `terraform fmt -check` と `terraform validate` を実行します（AWS には接続しません。`.github/workflows/ci.yml`）。
