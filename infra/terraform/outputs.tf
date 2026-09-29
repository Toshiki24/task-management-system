output "aws_account_id" {
  description = "Terraform を実行しているAWSアカウントのID。plan / apply の前に、意図したアカウントか確認する"
  value       = data.aws_caller_identity.current.account_id
}

output "aws_region" {
  description = "リソースを作成するリージョン"
  value       = var.aws_region
}
