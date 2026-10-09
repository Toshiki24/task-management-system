# Lambda の実行ロール。設計: aws-architecture.md 4.9。最小権限とする。

data "aws_iam_policy_document" "lambda_assume" {
  statement {
    actions = ["sts:AssumeRole"]
    principals {
      type        = "Service"
      identifiers = ["lambda.amazonaws.com"]
    }
  }
}

# --- API用 Lambda の実行ロール ---
resource "aws_iam_role" "api_lambda" {
  name               = "${local.name_prefix}-api-lambda"
  assume_role_policy = data.aws_iam_policy_document.lambda_assume.json
  tags               = { Name = "${local.name_prefix}-api-lambda" }
}

# VPC 内での実行(ENIの作成等)と CloudWatch Logs への出力
resource "aws_iam_role_policy_attachment" "api_lambda_vpc" {
  role       = aws_iam_role.api_lambda.name
  policy_arn = "arn:aws:iam::aws:policy/service-role/AWSLambdaVPCAccessExecutionRole"
}

# API用シークレットと Git 連携用シークレットの読み取りのみ(他のシークレット・サービスへはアクセスさせない)
data "aws_iam_policy_document" "api_lambda_secret" {
  statement {
    actions = ["secretsmanager:GetSecretValue"]
    resources = [
      aws_secretsmanager_secret.api.arn,
      aws_secretsmanager_secret.git.arn,
    ]
  }
}

resource "aws_iam_role_policy" "api_lambda_secret" {
  name   = "read-api-secret"
  role   = aws_iam_role.api_lambda.id
  policy = data.aws_iam_policy_document.api_lambda_secret.json
}

# --- マイグレーション用 Lambda の実行ロール ---
resource "aws_iam_role" "migration_lambda" {
  name               = "${local.name_prefix}-migration-lambda"
  assume_role_policy = data.aws_iam_policy_document.lambda_assume.json
  tags               = { Name = "${local.name_prefix}-migration-lambda" }
}

resource "aws_iam_role_policy_attachment" "migration_lambda_vpc" {
  role       = aws_iam_role.migration_lambda.name
  policy_arn = "arn:aws:iam::aws:policy/service-role/AWSLambdaVPCAccessExecutionRole"
}

# API用シークレットに加え、RDS が管理するマスターユーザーのシークレットも読み取る(アプリ用DBユーザー作成のため)
data "aws_iam_policy_document" "migration_lambda_secret" {
  statement {
    actions = ["secretsmanager:GetSecretValue"]
    resources = [
      aws_secretsmanager_secret.api.arn,
      aws_db_instance.main.master_user_secret[0].secret_arn,
    ]
  }
}

resource "aws_iam_role_policy" "migration_lambda_secret" {
  name   = "read-secrets"
  role   = aws_iam_role.migration_lambda.id
  policy = data.aws_iam_policy_document.migration_lambda_secret.json
}
