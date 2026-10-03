# 監視・通知・監査(SNS 通知、AWS Budgets、CloudTrail)。
# 設計: aws-architecture.md 4.10・5.1。CloudTrail の S3 証跡・ログ整合性検証は security-review-2.md SEC2-09 の上積み
# (元の設計は 90日のイベント履歴のみ。侵害時の追跡・改ざん検知を強化する)。

# --- SNS(アラーム・予算超過の通知) ---
resource "aws_sns_topic" "alerts" {
  name = "${local.name_prefix}-alerts"
  tags = { Name = "${local.name_prefix}-alerts" }
}

resource "aws_sns_topic_subscription" "alerts_email" {
  topic_arn = aws_sns_topic.alerts.arn
  protocol  = "email"
  endpoint  = var.alert_email
}

# CloudWatch アラームと AWS Budgets が、このトピックへ発行できるようにする
data "aws_iam_policy_document" "alerts_topic" {
  # アカウント所有者による管理権限(既定ポリシーを置き換えるため、管理権限を明示的に残す)
  statement {
    sid     = "AllowAccountManage"
    actions = ["SNS:Publish", "SNS:Subscribe", "SNS:GetTopicAttributes", "SNS:SetTopicAttributes"]
    principals {
      type        = "AWS"
      identifiers = ["arn:aws:iam::${data.aws_caller_identity.current.account_id}:root"]
    }
    resources = [aws_sns_topic.alerts.arn]
  }

  statement {
    sid     = "AllowCloudWatchAlarms"
    actions = ["SNS:Publish"]
    principals {
      type        = "Service"
      identifiers = ["cloudwatch.amazonaws.com"]
    }
    resources = [aws_sns_topic.alerts.arn]
  }

  statement {
    sid     = "AllowBudgets"
    actions = ["SNS:Publish"]
    principals {
      type        = "Service"
      identifiers = ["budgets.amazonaws.com"]
    }
    resources = [aws_sns_topic.alerts.arn]
  }
}

resource "aws_sns_topic_policy" "alerts" {
  arn    = aws_sns_topic.alerts.arn
  policy = data.aws_iam_policy_document.alerts_topic.json
}

# --- AWS Budgets(想定外の課金の検知。月40ドル) ---
resource "aws_budgets_budget" "monthly" {
  name         = "${local.name_prefix}-monthly"
  budget_type  = "COST"
  limit_amount = "40"
  limit_unit   = "USD"
  time_unit    = "MONTHLY"

  # 実績が予算の80%を超えたら通知
  notification {
    comparison_operator       = "GREATER_THAN"
    threshold                 = 80
    threshold_type            = "PERCENTAGE"
    notification_type         = "ACTUAL"
    subscriber_sns_topic_arns = [aws_sns_topic.alerts.arn]
  }

  # 予測が予算の100%を超えたら通知
  notification {
    comparison_operator       = "GREATER_THAN"
    threshold                 = 100
    threshold_type            = "PERCENTAGE"
    notification_type         = "FORECASTED"
    subscriber_sns_topic_arns = [aws_sns_topic.alerts.arn]
  }

  depends_on = [aws_sns_topic_policy.alerts]
}

# --- CloudTrail(監査ログ。S3 保存＋ログ整合性検証。security-review-2.md SEC2-09) ---
# 証跡のARNは bucket policy と循環参照になるため、既知の命名から文字列で組み立てる
locals {
  cloudtrail_name = "${local.name_prefix}-trail"
  cloudtrail_arn  = "arn:aws:cloudtrail:${var.aws_region}:${data.aws_caller_identity.current.account_id}:trail/${local.cloudtrail_name}"
}

resource "aws_s3_bucket" "cloudtrail" {
  bucket = "${local.name_prefix}-cloudtrail-${data.aws_caller_identity.current.account_id}"
  tags   = { Name = "${local.name_prefix}-cloudtrail" }
}

resource "aws_s3_bucket_public_access_block" "cloudtrail" {
  bucket                  = aws_s3_bucket.cloudtrail.id
  block_public_acls       = true
  block_public_policy     = true
  ignore_public_acls      = true
  restrict_public_buckets = true
}

resource "aws_s3_bucket_server_side_encryption_configuration" "cloudtrail" {
  bucket = aws_s3_bucket.cloudtrail.id
  rule {
    apply_server_side_encryption_by_default {
      sse_algorithm = "AES256"
    }
  }
}

# 保存容量の課金を抑えるため、一定期間でログを削除する
resource "aws_s3_bucket_lifecycle_configuration" "cloudtrail" {
  bucket = aws_s3_bucket.cloudtrail.id
  rule {
    id     = "expire-logs"
    status = "Enabled"
    filter {}
    expiration {
      days = var.cloudtrail_retention_days
    }
  }
}

# CloudTrail がこのバケットに書き込めるようにする(標準のバケットポリシー)
data "aws_iam_policy_document" "cloudtrail_bucket" {
  statement {
    sid     = "AWSCloudTrailAclCheck"
    actions = ["s3:GetBucketAcl"]
    principals {
      type        = "Service"
      identifiers = ["cloudtrail.amazonaws.com"]
    }
    resources = [aws_s3_bucket.cloudtrail.arn]
    condition {
      test     = "StringEquals"
      variable = "aws:SourceArn"
      values   = [local.cloudtrail_arn]
    }
  }

  statement {
    sid     = "AWSCloudTrailWrite"
    actions = ["s3:PutObject"]
    principals {
      type        = "Service"
      identifiers = ["cloudtrail.amazonaws.com"]
    }
    resources = ["${aws_s3_bucket.cloudtrail.arn}/AWSLogs/${data.aws_caller_identity.current.account_id}/*"]
    condition {
      test     = "StringEquals"
      variable = "s3:x-amz-acl"
      values   = ["bucket-owner-full-control"]
    }
    condition {
      test     = "StringEquals"
      variable = "aws:SourceArn"
      values   = [local.cloudtrail_arn]
    }
  }
}

resource "aws_s3_bucket_policy" "cloudtrail" {
  bucket = aws_s3_bucket.cloudtrail.id
  policy = data.aws_iam_policy_document.cloudtrail_bucket.json
}

resource "aws_cloudtrail" "main" {
  name           = local.cloudtrail_name
  s3_bucket_name = aws_s3_bucket.cloudtrail.id

  include_global_service_events = true
  is_multi_region_trail         = true
  # ログの改ざん検知(SEC2-09)
  enable_log_file_validation = true

  tags = { Name = local.cloudtrail_name }

  # バケットポリシーが先に適用されていないと作成に失敗する
  depends_on = [aws_s3_bucket_policy.cloudtrail]
}
