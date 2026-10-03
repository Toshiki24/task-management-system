# Amplify Hosting(画面 + BFF)。設計: aws-architecture.md 4.7・4.11。
# GitHub App の連携と初回デプロイは作業者がコンソールで行う(手順15)。アクセストークンは Terraform に置かない。

# --- Amplify の SSR 実行ロール(compute role): BFF用シークレットの読み取りのみ(4.9) ---
data "aws_iam_policy_document" "amplify_assume" {
  statement {
    actions = ["sts:AssumeRole"]
    principals {
      type        = "Service"
      identifiers = ["amplify.amazonaws.com"]
    }
  }
}

resource "aws_iam_role" "amplify_compute" {
  name               = "${local.name_prefix}-amplify-compute"
  assume_role_policy = data.aws_iam_policy_document.amplify_assume.json
  tags               = { Name = "${local.name_prefix}-amplify-compute" }
}

data "aws_iam_policy_document" "amplify_compute" {
  statement {
    actions   = ["secretsmanager:GetSecretValue"]
    resources = [aws_secretsmanager_secret.bff.arn]
  }
}

resource "aws_iam_role_policy" "amplify_compute" {
  name   = "read-bff-secret"
  role   = aws_iam_role.amplify_compute.id
  policy = data.aws_iam_policy_document.amplify_compute.json
}

# --- Amplify アプリ ---
resource "aws_amplify_app" "main" {
  name       = local.name_prefix
  repository = "https://github.com/${var.github_repository}"
  platform   = "WEB_COMPUTE"

  # SSR 実行ロール(BFF が実行時に Secrets Manager を読むため)
  compute_role_arn = aws_iam_role.amplify_compute.arn

  # 秘密情報そのものは置かない(名前・URLのみ)。SESSION_SECRET 等は実行時にロールで読む
  environment_variables = {
    API_BASE_URL  = "${aws_apigatewayv2_api.http.api_endpoint}/api"
    BFF_SECRET_ID = aws_secretsmanager_secret.bff.name
  }

  # ビルド設定はリポジトリルートの amplify.yml を使う(モノレポ・Node.js 24。aws-architecture.md 4.7・12章)

  # GitHub App 連携(アクセストークン)はコンソールで行うため、Terraform の差分対象から外す
  lifecycle {
    ignore_changes = [access_token, oauth_token]
  }

  tags = { Name = local.name_prefix }
}

# --- main ブランチ(自動ビルド・デプロイ) ---
resource "aws_amplify_branch" "main" {
  app_id      = aws_amplify_app.main.id
  branch_name = "main"
  framework   = "Next.js - SSR"
  stage       = "PRODUCTION"

  enable_auto_build = true

  tags = { Name = "${local.name_prefix}-main" }
}

# --- カスタムドメイン(tms.accent24.jp)。証明書は Amplify(ACM)が管理 ---
# DNS はムームーDNSで手動設定するため、検証完了を待たない(wait_for_verification=false)。
# apply 後に出力される CNAME をムームーDNSに追加する(手順16)。
resource "aws_amplify_domain_association" "main" {
  app_id                = aws_amplify_app.main.id
  domain_name           = var.app_domain
  wait_for_verification = false

  sub_domain {
    branch_name = aws_amplify_branch.main.branch_name
    prefix      = ""
  }
}
