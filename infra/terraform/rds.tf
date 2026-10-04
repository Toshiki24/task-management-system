# RDS(PostgreSQL)。設計: aws-architecture.md 4.3・4.10。
# - プライベートサブネットのみ、パブリックアクセス無効、保存時暗号化、TLS必須。
# - マスターユーザーのパスワードは RDS が Secrets Manager で管理し、Terraform の state に残さない。
# - アプリ用DBユーザー(tms_app)は Terraform では作らない。マイグレーション用 Lambda で作成する(4.4)。

resource "aws_db_subnet_group" "main" {
  name       = "${local.name_prefix}-db-subnets"
  subnet_ids = [for s in aws_subnet.private : s.id]
  tags       = { Name = "${local.name_prefix}-db-subnets" }
}

# TLS 接続を必須にする(rds.force_ssl=1)
resource "aws_db_parameter_group" "postgres16" {
  name        = "${local.name_prefix}-postgres16"
  family      = "postgres16"
  description = "Force SSL for ${local.name_prefix}"

  parameter {
    name  = "rds.force_ssl"
    value = "1"
    # rds.force_ssl は再起動が必要な static パラメータ。AWS 側も pending-reboot で保持するため
    # 明示して apply のたびに差分(immediate への変更)が出ないようにする。
    apply_method = "pending-reboot"
  }

  tags = { Name = "${local.name_prefix}-postgres16" }
}

resource "aws_db_instance" "main" {
  identifier     = "${local.name_prefix}-db"
  engine         = "postgres"
  engine_version = "16"
  instance_class = "db.t4g.micro"

  # ストレージ: gp3 20GB、自動拡張なし(想定外の課金を防ぐ。空き容量はアラームで監視)
  allocated_storage = 20
  storage_type      = "gp3"
  storage_encrypted = true

  db_name  = "task_management"
  username = "tms_admin"
  # パスワードは RDS が Secrets Manager で管理する(state に残さない。ローテーションも RDS が行う)
  manage_master_user_password = true

  db_subnet_group_name   = aws_db_subnet_group.main.name
  vpc_security_group_ids = [aws_security_group.rds.id]
  parameter_group_name   = aws_db_parameter_group.postgres16.name
  multi_az               = false
  publicly_accessible    = false

  # バックアップ(7日・PITR)。メンテナンス/バックアップ時間は UTC(日曜3-4時JST = 土曜18-19時UTC)
  backup_retention_period = 7
  backup_window           = "17:00-17:30"
  maintenance_window      = "sat:18:00-sat:19:00"
  copy_tags_to_snapshot   = true

  # マイナーバージョンの自動更新(セキュリティ修正の適用)
  auto_minor_version_upgrade = true

  # 誤削除の防止(削除保護＋削除時に最終スナップショット)
  deletion_protection       = true
  skip_final_snapshot       = false
  final_snapshot_identifier = "${local.name_prefix}-db-final"

  # 費用を抑えるため無効(基本メトリクスは CloudWatch で確認)
  performance_insights_enabled = false
  monitoring_interval          = 0

  tags = { Name = "${local.name_prefix}-db" }

  # 自動マイナー更新で engine_version が変わっても、Terraform が元に戻そうとしないようにする
  lifecycle {
    ignore_changes = [engine_version]
  }
}

# --- RDS アラーム(SNS 通知。aws-architecture.md 4.10) ---
resource "aws_cloudwatch_metric_alarm" "rds_cpu" {
  alarm_name          = "${local.name_prefix}-rds-cpu"
  namespace           = "AWS/RDS"
  metric_name         = "CPUUtilization"
  statistic           = "Average"
  period              = 900 # 15分
  evaluation_periods  = 1
  threshold           = 80
  comparison_operator = "GreaterThanThreshold"
  alarm_description   = "RDS CPU使用率が15分平均で80%以上"
  dimensions          = { DBInstanceIdentifier = aws_db_instance.main.identifier }
  alarm_actions       = [aws_sns_topic.alerts.arn]
  tags                = { Name = "${local.name_prefix}-rds-cpu" }
}

resource "aws_cloudwatch_metric_alarm" "rds_free_storage" {
  alarm_name          = "${local.name_prefix}-rds-free-storage"
  namespace           = "AWS/RDS"
  metric_name         = "FreeStorageSpace"
  statistic           = "Average"
  period              = 300
  evaluation_periods  = 1
  threshold           = 2 * 1024 * 1024 * 1024 # 2GB(バイト)
  comparison_operator = "LessThanThreshold"
  alarm_description   = "RDS の空きストレージが2GB未満(自動拡張なしのため)"
  dimensions          = { DBInstanceIdentifier = aws_db_instance.main.identifier }
  alarm_actions       = [aws_sns_topic.alerts.arn]
  tags                = { Name = "${local.name_prefix}-rds-free-storage" }
}
