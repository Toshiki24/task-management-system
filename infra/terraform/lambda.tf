# Lambda(API用・マイグレーション用)。設計: aws-architecture.md 4.4・4.10。
# 同じコンテナイメージ(ECR)を使い、マイグレーション用は実行コマンドを変える。

locals {
  # Lambda のイメージURI(初回は api_image_tag、以後は GitHub Actions が UpdateFunctionCode で更新)
  lambda_image_uri = "${aws_ecr_repository.api.repository_url}:${var.api_image_tag}"
}

# --- ロググループ(保存30日。Lambda が自動作成する前に作り、保存期間を設定する) ---
resource "aws_cloudwatch_log_group" "api_lambda" {
  name              = "/aws/lambda/${local.name_prefix}-api"
  retention_in_days = 30
  tags              = { Name = "${local.name_prefix}-api" }
}

resource "aws_cloudwatch_log_group" "migration_lambda" {
  name              = "/aws/lambda/${local.name_prefix}-migration"
  retention_in_days = 30
  tags              = { Name = "${local.name_prefix}-migration" }
}

# --- API用 Lambda ---
resource "aws_lambda_function" "api" {
  function_name = "${local.name_prefix}-api"
  role          = aws_iam_role.api_lambda.arn
  package_type  = "Image"
  image_uri     = local.lambda_image_uri
  architectures = ["x86_64"]
  memory_size   = 1024
  timeout       = 30

  # 大量リクエストによる費用増加・RDS接続枯渇を防ぐ(4.4)
  reserved_concurrent_executions = 10

  vpc_config {
    subnet_ids         = [for s in aws_subnet.private : s.id]
    security_group_ids = [aws_security_group.lambda.id]
  }

  environment {
    variables = {
      APP_SECRET_ID = aws_secretsmanager_secret.api.name
      # Git 連携(M4)。Webhook 署名シークレットや GitHub App 秘密鍵をここから読む
      GIT_SECRET_ID          = aws_secretsmanager_secret.git.name
      ASPNETCORE_ENVIRONMENT = "Production"
      # 通知メール(M3 step6)。FromAddress があればアプリは SES で実送信する(無ければログ出力)
      Email__FromAddress = var.ses_from_address
      App__BaseUrl       = "https://${var.app_domain}"
    }
  }

  depends_on = [aws_cloudwatch_log_group.api_lambda]

  # イメージは GitHub Actions が更新するため、Terraform は差分を無視する
  lifecycle {
    ignore_changes = [image_uri]
  }

  tags = { Name = "${local.name_prefix}-api" }
}

# API Gateway のみに API用 Lambda の呼び出しを許可する(4.4 呼び出し元)
resource "aws_lambda_permission" "api_gateway" {
  statement_id  = "AllowApiGatewayInvoke"
  action        = "lambda:InvokeFunction"
  function_name = aws_lambda_function.api.function_name
  principal     = "apigateway.amazonaws.com"
  source_arn    = "${aws_apigatewayv2_api.http.execution_arn}/*"
}

# --- マイグレーション用 Lambda(作業者が手動実行) ---
resource "aws_lambda_function" "migration" {
  function_name = "${local.name_prefix}-migration"
  role          = aws_iam_role.migration_lambda.arn
  package_type  = "Image"
  image_uri     = local.lambda_image_uri
  architectures = ["x86_64"]
  memory_size   = 1024
  timeout       = 300

  reserved_concurrent_executions = 1

  vpc_config {
    subnet_ids         = [for s in aws_subnet.private : s.id]
    security_group_ids = [aws_security_group.lambda.id]
  }

  environment {
    variables = {
      APP_SECRET_ID          = aws_secretsmanager_secret.api.name
      ASPNETCORE_ENVIRONMENT = "Production"
      # 同じイメージで、APIホストではなくマイグレーション処理を実行する(Program.cs の分岐。aws-architecture.md 4.4)
      MIGRATION_MODE = "true"
      # RDS マスターユーザーのシークレット名(アプリ用DBユーザー作成に使う)
      RDS_MASTER_SECRET_ID = aws_db_instance.main.master_user_secret[0].secret_arn
    }
  }

  depends_on = [aws_cloudwatch_log_group.migration_lambda]

  lifecycle {
    ignore_changes = [image_uri]
  }

  tags = { Name = "${local.name_prefix}-migration" }
}

# --- Lambda のエラーアラーム(4.10) ---
resource "aws_cloudwatch_metric_alarm" "api_lambda_errors" {
  alarm_name          = "${local.name_prefix}-api-lambda-errors"
  namespace           = "AWS/Lambda"
  metric_name         = "Errors"
  statistic           = "Sum"
  period              = 300
  evaluation_periods  = 1
  threshold           = 1
  comparison_operator = "GreaterThanOrEqualToThreshold"
  alarm_description   = "API用Lambdaのエラーが5分間で1件以上"
  dimensions          = { FunctionName = aws_lambda_function.api.function_name }
  alarm_actions       = [aws_sns_topic.alerts.arn]
  tags                = { Name = "${local.name_prefix}-api-lambda-errors" }
}
