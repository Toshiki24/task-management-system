# ネットワーク(VPC・サブネット・セキュリティグループ・VPCエンドポイント)。
# 設計: aws-architecture.md 4.2。
# - プライベートサブネットのみ。インターネットゲートウェイ・NATは作らない(VPC内のリソースは外部に出ない)。
# - RDS・Lambda はプライベートサブネットに配置。Secrets Manager へは VPC エンドポイント経由で接続する。
# - 送信元・送信先は IP ではなくセキュリティグループで指定し、Lambda 以外から RDS・エンドポイントに接続させない。

locals {
  vpc_cidr = "10.0.0.0/16"

  # RDS のサブネットグループには2つのAZのサブネットが必要なため、2AZに1つずつ作る。
  # 設計(4.2)の 10.0.1.0/24(1a) / 10.0.2.0/24(1c) に合わせる。
  private_subnets = {
    a = { cidr = "10.0.1.0/24", az = "${var.aws_region}a" }
    c = { cidr = "10.0.2.0/24", az = "${var.aws_region}c" }
  }
}

resource "aws_vpc" "main" {
  cidr_block = local.vpc_cidr
  # VPC エンドポイントのプライベートDNS(private_dns_enabled)を使うために必要
  enable_dns_support   = true
  enable_dns_hostnames = true

  tags = { Name = "${local.name_prefix}-vpc" }
}

resource "aws_subnet" "private" {
  for_each = local.private_subnets

  vpc_id            = aws_vpc.main.id
  cidr_block        = each.value.cidr
  availability_zone = each.value.az
  # パブリックIPは割り当てない(インターネットに出る経路を作らない)
  map_public_ip_on_launch = false

  tags = { Name = "${local.name_prefix}-private-${each.key}" }
}

# --- セキュリティグループ ---
# 循環参照(Lambda→RDS と RDS←Lambda)を避けるため、ルールは別リソースで定義する。

resource "aws_security_group" "lambda" {
  name        = "${local.name_prefix}-lambda-sg"
  description = "API Lambda. Outbound to RDS and Secrets Manager VPC endpoint only."
  vpc_id      = aws_vpc.main.id

  tags = { Name = "${local.name_prefix}-lambda-sg" }
}

resource "aws_security_group" "rds" {
  name        = "${local.name_prefix}-rds-sg"
  description = "RDS PostgreSQL. Inbound from Lambda SG only."
  vpc_id      = aws_vpc.main.id

  tags = { Name = "${local.name_prefix}-rds-sg" }
}

resource "aws_security_group" "endpoints" {
  name        = "${local.name_prefix}-endpoints-sg"
  description = "Interface VPC endpoints. Inbound HTTPS from Lambda SG only."
  vpc_id      = aws_vpc.main.id

  tags = { Name = "${local.name_prefix}-endpoints-sg" }
}

# Lambda → RDS(5432)
resource "aws_vpc_security_group_egress_rule" "lambda_to_rds" {
  security_group_id            = aws_security_group.lambda.id
  referenced_security_group_id = aws_security_group.rds.id
  ip_protocol                  = "tcp"
  from_port                    = 5432
  to_port                      = 5432
  description                  = "PostgreSQL to RDS"
}

# Lambda → VPCエンドポイント(443)
resource "aws_vpc_security_group_egress_rule" "lambda_to_endpoints" {
  security_group_id            = aws_security_group.lambda.id
  referenced_security_group_id = aws_security_group.endpoints.id
  ip_protocol                  = "tcp"
  from_port                    = 443
  to_port                      = 443
  description                  = "HTTPS to interface VPC endpoints"
}

# RDS ← Lambda(5432)
resource "aws_vpc_security_group_ingress_rule" "rds_from_lambda" {
  security_group_id            = aws_security_group.rds.id
  referenced_security_group_id = aws_security_group.lambda.id
  ip_protocol                  = "tcp"
  from_port                    = 5432
  to_port                      = 5432
  description                  = "PostgreSQL from Lambda"
}

# エンドポイント ← Lambda(443)
resource "aws_vpc_security_group_ingress_rule" "endpoints_from_lambda" {
  security_group_id            = aws_security_group.endpoints.id
  referenced_security_group_id = aws_security_group.lambda.id
  ip_protocol                  = "tcp"
  from_port                    = 443
  to_port                      = 443
  description                  = "HTTPS from Lambda"
}

# --- VPCエンドポイント(Secrets Manager) ---
# Lambda が VPC 内から Secrets Manager を読むため。起動時にのみ読むため1AZで十分(設計 4.2)。
resource "aws_vpc_endpoint" "secretsmanager" {
  vpc_id              = aws_vpc.main.id
  service_name        = "com.amazonaws.${var.aws_region}.secretsmanager"
  vpc_endpoint_type   = "Interface"
  subnet_ids          = [aws_subnet.private["a"].id]
  security_group_ids  = [aws_security_group.endpoints.id]
  private_dns_enabled = true

  tags = { Name = "${local.name_prefix}-secretsmanager-endpoint" }
}
