variable "aws_region" {
  description = "リソースを作成するリージョン"
  type        = string
  default     = "ap-northeast-1"
}

variable "aws_profile" {
  description = "使用する AWS CLI のプロファイル名(aws configure --profile で作成したもの)。null の場合は既定の認証情報を使う"
  type        = string
  default     = null
}

variable "project_name" {
  description = "リソース名・タグに使うプロジェクト名"
  type        = string
  default     = "task-management-system"
}

variable "environment" {
  description = "環境名(リソース名・タグに使う)"
  type        = string
  default     = "prod"
}

variable "app_domain" {
  description = "アプリを公開するドメイン(Amplify のカスタムドメイン。DNSはムームーDNSでCNAMEを設定する)"
  type        = string
  default     = "tms.accent24.jp"
}

variable "alert_email" {
  description = "アラーム・予算超過の通知先メールアドレス。秘密ではないが個人情報のため terraform.tfvars で指定する(リポジトリには置かない)"
  type        = string
}

variable "cloudtrail_retention_days" {
  description = "CloudTrail のログ(S3)の保存日数。既定のイベント履歴(90日)より長く保持しつつ、保存容量の課金を抑える(security-review-2.md SEC2-09)"
  type        = number
  default     = 365
}
