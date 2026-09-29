provider "aws" {
  # profile が未指定(null)の場合は、環境変数 AWS_PROFILE や既定のプロファイルを使う
  region  = var.aws_region
  profile = var.aws_profile

  # このアプリのリソースに共通のタグを付け、既存のリソースと区別し、請求画面でこのアプリの費用を確認できるようにする
  default_tags {
    tags = {
      Project     = var.project_name
      Environment = var.environment
      ManagedBy   = "terraform"
    }
  }
}
