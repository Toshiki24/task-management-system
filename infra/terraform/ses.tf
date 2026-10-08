# メール送信(Amazon SES v2)。Phase 2 M3 step6 の通知メールで使う。
# 送信ドメイン identity + Easy DKIM を作成し、API Lambda に ses:SendEmail を最小権限で付与する。
#
# apply で自動化できるのはここまで。以下は AWS の仕様上、手動手順が残る(README 参照):
#   1) 出力される DKIM 用 CNAME(3件)を DNS(ムームーDNS)へ登録 → これで identity 検証が完了する
#   2) SES サンドボックス解除(本番送信枠)の申請 … AWS サポートへの申請で、Terraform では不可
# 検証・解除が未完でも、アプリ側は送信失敗をログに記録して処理を継続する(アプリ内通知は保存済み)。

resource "aws_sesv2_email_identity" "main" {
  email_identity = var.app_domain

  # Easy DKIM(2048bit)。DKIM 用 CNAME を DNS に登録するとドメインが検証済みになる
  dkim_signing_attributes {
    next_signing_key_length = "RSA_2048_BIT"
  }

  tags = { Name = "${local.name_prefix}-ses" }
}

# DNS(ムームーDNS)へ手動登録する DKIM 用 CNAME レコード(3件)
locals {
  ses_dkim_cname_records = [
    for token in aws_sesv2_email_identity.main.dkim_signing_attributes[0].tokens : {
      name  = "${token}._domainkey.${var.app_domain}"
      value = "${token}.dkim.amazonses.com"
    }
  ]
}

# API 用 Lambda に、この identity からの送信のみを許可する(最小権限)
data "aws_iam_policy_document" "api_lambda_ses" {
  statement {
    actions   = ["ses:SendEmail"]
    resources = [aws_sesv2_email_identity.main.arn]
  }
}

resource "aws_iam_role_policy" "api_lambda_ses" {
  name   = "send-email"
  role   = aws_iam_role.api_lambda.id
  policy = data.aws_iam_policy_document.api_lambda_ses.json
}
