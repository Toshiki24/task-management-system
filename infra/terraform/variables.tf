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
