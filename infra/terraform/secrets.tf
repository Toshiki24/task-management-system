# Secrets Manager の「入れ物」。設計: aws-architecture.md 4.8。
# 重要: Terraform は入れ物(シークレット)のみを作成し、値(バージョン)は作成しない。
#       値は作業者が AWS CLI / コンソールで手動登録する(秘密情報を Terraform のコード・変数・state に含めないため)。
#       RDS マスターユーザーのシークレットは RDS が自動作成する(rds.tf の manage_master_user_password)。

# API(Lambda)用: Jwt:Key / ConnectionStrings:DefaultConnection / OriginVerify:Secret
resource "aws_secretsmanager_secret" "api" {
  name        = "${var.project_name}/${var.environment}/api"
  description = "API(Lambda)用。Jwt:Key / ConnectionStrings:DefaultConnection / OriginVerify:Secret。値は作業者が手動登録する"
  tags        = { Name = "${local.name_prefix}-api-secret" }
}

# BFF(Amplify SSR)用: SESSION_SECRET / ORIGIN_VERIFY_SECRET(API用と同じ値)
resource "aws_secretsmanager_secret" "bff" {
  name        = "${var.project_name}/${var.environment}/bff"
  description = "BFF(Amplify SSR)用。SESSION_SECRET / ORIGIN_VERIFY_SECRET。値は作業者が手動登録する"
  tags        = { Name = "${local.name_prefix}-bff-secret" }
}
