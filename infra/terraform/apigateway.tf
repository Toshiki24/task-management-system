# API Gateway(HTTP API)。設計: aws-architecture.md 4.5・4.10。
# ブラウザからは直接呼ばれない(BFF 経由)。CORS は設定しない。ルーティングは ASP.NET Core が行う。

resource "aws_apigatewayv2_api" "http" {
  name          = "${local.name_prefix}-api"
  protocol_type = "HTTP"
  # CORS は設定しない(ブラウザから直接呼び出さないため。API詳細仕様書 6.3)
  tags = { Name = "${local.name_prefix}-api" }
}

resource "aws_apigatewayv2_integration" "api" {
  api_id                 = aws_apigatewayv2_api.http.id
  integration_type       = "AWS_PROXY"
  integration_uri        = aws_lambda_function.api.invoke_arn
  integration_method     = "POST"
  payload_format_version = "2.0"
}

# すべてのパスを API用 Lambda に流す(ルーティングは ASP.NET Core)
resource "aws_apigatewayv2_route" "default" {
  api_id    = aws_apigatewayv2_api.http.id
  route_key = "$default"
  target    = "integrations/${aws_apigatewayv2_integration.api.id}"
}

# ログインの総当たり対策として、認証系パスは個別に低いスロットリングをかける(security-review-2.md SEC2-01)。
# $default とは別に明示ルートを作り、ステージの route_settings で上限を下げる。
resource "aws_apigatewayv2_route" "auth_login" {
  api_id    = aws_apigatewayv2_api.http.id
  route_key = "POST /api/auth/login"
  target    = "integrations/${aws_apigatewayv2_integration.api.id}"
}

resource "aws_apigatewayv2_route" "auth_refresh" {
  api_id    = aws_apigatewayv2_api.http.id
  route_key = "POST /api/auth/refresh"
  target    = "integrations/${aws_apigatewayv2_integration.api.id}"
}

# アクセスログ用ロググループ(30日)
resource "aws_cloudwatch_log_group" "apigw_access" {
  name              = "/aws/apigateway/${local.name_prefix}"
  retention_in_days = 30
  tags              = { Name = "${local.name_prefix}-apigw" }
}

resource "aws_apigatewayv2_stage" "default" {
  api_id      = aws_apigatewayv2_api.http.id
  name        = "$default"
  auto_deploy = true

  # ステージ全体のスロットリング(毎秒20・バースト40。4.5)
  default_route_settings {
    throttling_rate_limit  = 20
    throttling_burst_limit = 40
  }

  # 認証系パスはより低い上限にする(SEC2-01)
  route_settings {
    route_key              = aws_apigatewayv2_route.auth_login.route_key
    throttling_rate_limit  = 5
    throttling_burst_limit = 10
  }

  route_settings {
    route_key              = aws_apigatewayv2_route.auth_refresh.route_key
    throttling_rate_limit  = 5
    throttling_burst_limit = 10
  }

  # アクセスログ(JSON)。ヘッダー・本文は出さない(JWT等の秘密情報を記録しない。4.5・基本設計書18章)
  access_log_settings {
    destination_arn = aws_cloudwatch_log_group.apigw_access.arn
    format = jsonencode({
      requestId      = "$context.requestId"
      sourceIp       = "$context.identity.sourceIp"
      httpMethod     = "$context.httpMethod"
      path           = "$context.path"
      status         = "$context.status"
      responseLength = "$context.responseLength"
      responseTime   = "$context.responseLatency"
    })
  }

  tags = { Name = "${local.name_prefix}-api" }
}

# --- API Gateway のアラーム(4.10) ---
resource "aws_cloudwatch_metric_alarm" "apigw_5xx" {
  alarm_name          = "${local.name_prefix}-apigw-5xx"
  namespace           = "AWS/ApiGateway"
  metric_name         = "5xx"
  statistic           = "Sum"
  period              = 300
  evaluation_periods  = 1
  threshold           = 5
  comparison_operator = "GreaterThanOrEqualToThreshold"
  alarm_description   = "API Gateway の 5xx が5分間で5件以上"
  dimensions          = { ApiId = aws_apigatewayv2_api.http.id }
  alarm_actions       = [aws_sns_topic.alerts.arn]
  tags                = { Name = "${local.name_prefix}-apigw-5xx" }
}

# 4xx の急増(不正アクセス・総当たりの検知)。SEC2-03 の低速スプレーも、閾値内だが本アラームで傾向を把握する
resource "aws_cloudwatch_metric_alarm" "apigw_4xx" {
  alarm_name          = "${local.name_prefix}-apigw-4xx"
  namespace           = "AWS/ApiGateway"
  metric_name         = "4xx"
  statistic           = "Sum"
  period              = 300
  evaluation_periods  = 1
  threshold           = 100
  comparison_operator = "GreaterThanOrEqualToThreshold"
  alarm_description   = "API Gateway の 4xx が5分間で100件以上(不正アクセスの検知)"
  dimensions          = { ApiId = aws_apigatewayv2_api.http.id }
  alarm_actions       = [aws_sns_topic.alerts.arn]
  tags                = { Name = "${local.name_prefix}-apigw-4xx" }
}

# ログインの401をアクセスログから数え、低速スプレーを 4xx 急増アラームより低い閾値で検知する(security-review-2.md SEC2-03)
resource "aws_cloudwatch_log_metric_filter" "login_401" {
  name           = "${local.name_prefix}-login-401"
  log_group_name = aws_cloudwatch_log_group.apigw_access.name
  # アクセスログ(JSON)で、ログインパスかつステータス401のものを数える
  pattern = "{ $.status = \"401\" && $.path = \"*auth/login\" }"

  metric_transformation {
    name          = "${local.name_prefix}-login-401"
    namespace     = "TaskManagement/Security"
    value         = "1"
    default_value = "0"
  }
}

resource "aws_cloudwatch_metric_alarm" "login_401" {
  alarm_name          = "${local.name_prefix}-login-401-spike"
  namespace           = "TaskManagement/Security"
  metric_name         = aws_cloudwatch_log_metric_filter.login_401.metric_transformation[0].name
  statistic           = "Sum"
  period              = 300
  evaluation_periods  = 1
  threshold           = 20
  comparison_operator = "GreaterThanOrEqualToThreshold"
  alarm_description   = "ログインの401が5分間で20件以上(低速スプレーの検知。SEC2-03)"
  treat_missing_data  = "notBreaching"
  alarm_actions       = [aws_sns_topic.alerts.arn]
  tags                = { Name = "${local.name_prefix}-login-401-spike" }
}
