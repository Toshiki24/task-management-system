terraform {
  required_version = ">= 1.9"

  required_providers {
    aws = {
      source  = "hashicorp/aws"
      version = "~> 6.0"
    }
  }

  # state はローカル(このディレクトリの terraform.tfstate)に保存する。
  # 秘密情報(RDSのパスワード、Jwt:Key等)は state に含めない構成にしている(README.md 参照)。
  # GitHub Actions や複数のPCから実行する必要が出たら、S3 backend に移行する(terraform init -migrate-state)。
}
