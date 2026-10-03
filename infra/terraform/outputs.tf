output "aws_account_id" {
  description = "Terraform を実行しているAWSアカウントのID。plan / apply の前に、意図したアカウントか確認する"
  value       = data.aws_caller_identity.current.account_id
}

output "aws_region" {
  description = "リソースを作成するリージョン"
  value       = var.aws_region
}

# シークレットの「名前」は秘密情報ではない設定値(aws-architecture.md 2.3)。
# 作業者が値を登録する際(手順9)や、Lambda の APP_SECRET_ID / Amplify の BFF_SECRET_ID 設定に使う。
output "api_secret_name" {
  description = "API(Lambda)用シークレットの名前。値を手動登録する対象"
  value       = aws_secretsmanager_secret.api.name
}

output "bff_secret_name" {
  description = "BFF(Amplify SSR)用シークレットの名前。値を手動登録する対象"
  value       = aws_secretsmanager_secret.bff.name
}

output "rds_master_secret_arn" {
  description = "RDS が管理するマスターユーザーのシークレットARN。マイグレーション用 Lambda が読み取る"
  value       = aws_db_instance.main.master_user_secret[0].secret_arn
}

output "ecr_repository_url" {
  description = "API コンテナイメージの ECR リポジトリURL(GitHub Actions のビルド・プッシュ先)"
  value       = aws_ecr_repository.api.repository_url
}

output "github_actions_role_arn" {
  description = "GitHub Actions が OIDC で assume するロールARN(ワークフローの aws-actions/configure-aws-credentials に設定する)"
  value       = aws_iam_role.github_actions.arn
}

output "api_base_url" {
  description = "BFF(Amplify)の API_BASE_URL に設定する値(API Gateway の既定URL + /api)"
  value       = "${aws_apigatewayv2_api.http.api_endpoint}/api"
}

output "migration_lambda_name" {
  description = "マイグレーション用 Lambda の名前(aws lambda invoke で手動実行する)"
  value       = aws_lambda_function.migration.function_name
}

output "amplify_app_id" {
  description = "Amplify アプリのID(コンソールでの GitHub 連携・初回デプロイに使う)"
  value       = aws_amplify_app.main.id
}

output "amplify_default_domain" {
  description = "Amplify の既定ドメイン(動作確認用)"
  value       = aws_amplify_app.main.default_domain
}

output "amplify_cert_verification_dns_record" {
  description = "カスタムドメインの証明書検証用 DNS レコード。ムームーDNSに追加する(手順16)"
  value       = aws_amplify_domain_association.main.certificate_verification_dns_record
}

output "amplify_subdomain_dns_records" {
  description = "カスタムドメイン(tms.accent24.jp)の CNAME レコード。ムームーDNSに追加する(手順16)"
  value       = [for s in aws_amplify_domain_association.main.sub_domain : s.dns_record]
}
