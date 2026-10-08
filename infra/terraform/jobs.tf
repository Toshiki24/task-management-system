# スケジュール実行ジョブ(M3 step7 期限通知)。設計: m3-collaboration.md §7。
# API と同じコンテナイメージを JOB_MODE=due-notifications で起動し、EventBridge で日次実行する。

resource "aws_cloudwatch_log_group" "due_notifications_lambda" {
  name              = "/aws/lambda/${local.name_prefix}-due-notifications"
  retention_in_days = 30
  tags              = { Name = "${local.name_prefix}-due-notifications" }
}

# 期限通知ジョブ用 Lambda。DB 読み書きのため、マイグレーション用と同じロール(Secret 読取＋VPC)を使う
resource "aws_lambda_function" "due_notifications" {
  function_name = "${local.name_prefix}-due-notifications"
  role          = aws_iam_role.migration_lambda.arn
  package_type  = "Image"
  image_uri     = local.lambda_image_uri
  architectures = ["x86_64"]
  memory_size   = 1024
  timeout       = 120

  reserved_concurrent_executions = 1

  vpc_config {
    subnet_ids         = [for s in aws_subnet.private : s.id]
    security_group_ids = [aws_security_group.lambda.id]
  }

  environment {
    variables = {
      APP_SECRET_ID          = aws_secretsmanager_secret.api.name
      ASPNETCORE_ENVIRONMENT = "Production"
      # 同じイメージで、APIホストではなく期限通知ジョブを実行する(Program.cs の分岐)
      JOB_MODE = "due-notifications"
    }
  }

  depends_on = [aws_cloudwatch_log_group.due_notifications_lambda]

  lifecycle {
    ignore_changes = [image_uri]
  }

  tags = { Name = "${local.name_prefix}-due-notifications" }
}

# 日次スケジュール(既定 23:00 UTC = 08:00 JST)。cron は UTC
resource "aws_cloudwatch_event_rule" "due_notifications_daily" {
  name                = "${local.name_prefix}-due-notifications-daily"
  description         = "期限通知ジョブを日次で起動する(M3 §7)"
  schedule_expression = var.due_notification_schedule
  tags                = { Name = "${local.name_prefix}-due-notifications-daily" }
}

resource "aws_cloudwatch_event_target" "due_notifications" {
  rule      = aws_cloudwatch_event_rule.due_notifications_daily.name
  target_id = "due-notifications-lambda"
  arn       = aws_lambda_function.due_notifications.arn
  input     = "{}"
}

# EventBridge からの起動のみ許可する
resource "aws_lambda_permission" "due_notifications_events" {
  statement_id  = "AllowEventBridgeInvoke"
  action        = "lambda:InvokeFunction"
  function_name = aws_lambda_function.due_notifications.function_name
  principal     = "events.amazonaws.com"
  source_arn    = aws_cloudwatch_event_rule.due_notifications_daily.arn
}
