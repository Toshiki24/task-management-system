locals {
  # リソース名の接頭辞(例: task-management-system-prod-db)
  name_prefix = "${var.project_name}-${var.environment}"
}

# 実行しているAWSアカウントを確認するために使う(意図しないアカウントに作成しないため)
data "aws_caller_identity" "current" {}
