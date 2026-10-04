# RDS スナップショットの改ざん・削除耐性(AWS Backup + Vault Lock)。security-review-2.md SEC2-06。
# RDS の自動バックアップ(rds.tf)に加え、ロックした保管庫にも保持し、誤削除・不正削除への最終防衛線とする。
# Vault Lock は governance モード(可逆)とする。changeable_for_days を指定すると compliance(不可逆)に
# なるため指定しない。特別な権限を持つ主体のみロックを解除・変更できる。

data "aws_iam_policy_document" "backup_assume" {
  statement {
    actions = ["sts:AssumeRole"]
    principals {
      type        = "Service"
      identifiers = ["backup.amazonaws.com"]
    }
  }
}

resource "aws_iam_role" "backup" {
  name               = "${local.name_prefix}-backup"
  assume_role_policy = data.aws_iam_policy_document.backup_assume.json
  tags               = { Name = "${local.name_prefix}-backup" }
}

resource "aws_iam_role_policy_attachment" "backup" {
  role       = aws_iam_role.backup.name
  policy_arn = "arn:aws:iam::aws:policy/service-role/AWSBackupServiceRolePolicyForBackup"
}

resource "aws_backup_vault" "main" {
  name = "${local.name_prefix}-vault"
  tags = { Name = "${local.name_prefix}-vault" }
}

# Vault Lock(governance モード)。min/max の保持期間を強制する。changeable_for_days は指定しない(=可逆)
resource "aws_backup_vault_lock_configuration" "main" {
  backup_vault_name  = aws_backup_vault.main.name
  min_retention_days = 7
  max_retention_days = 365
}

resource "aws_backup_plan" "main" {
  name = "${local.name_prefix}-plan"

  rule {
    rule_name         = "daily"
    target_vault_name = aws_backup_vault.main.name
    # 毎日 16:00 UTC(01:00 JST)。RDS のバックアップ/メンテナンス時間と重ならない時間にする
    schedule = "cron(0 16 * * ? *)"

    lifecycle {
      # Vault Lock の min_retention_days(7) 以上。保管料を抑えるため30日で削除
      delete_after = 30
    }
  }

  tags = { Name = "${local.name_prefix}-plan" }
}

resource "aws_backup_selection" "rds" {
  name         = "${local.name_prefix}-rds"
  iam_role_arn = aws_iam_role.backup.arn
  plan_id      = aws_backup_plan.main.id
  resources    = [aws_db_instance.main.arn]
}
