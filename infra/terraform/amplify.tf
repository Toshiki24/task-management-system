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

# --- Amplify のサービスロール: ビルド・デプロイと CloudWatch Logs への出力(4.9) ---
resource "aws_iam_role" "amplify_service" {
  name               = "${local.name_prefix}-amplify-service"
  assume_role_policy = data.aws_iam_policy_document.amplify_assume.json
  tags               = { Name = "${local.name_prefix}-amplify-service" }
}

# Amplify のログ出力に必要な CloudWatch Logs 権限(Amplify のロググループに限定)
data "aws_iam_policy_document" "amplify_service" {
  statement {
    actions = [
      "logs:CreateLogGroup",
      "logs:CreateLogStream",
      "logs:PutLogEvents",
      "logs:DescribeLogGroups",
      "logs:DescribeLogStreams",
    ]
    resources = [
      "arn:aws:logs:${var.aws_region}:${data.aws_caller_identity.current.account_id}:log-group:/aws/amplify/*",
    ]
  }
}

resource "aws_iam_role_policy" "amplify_service" {
  name   = "amplify-logs"
  role   = aws_iam_role.amplify_service.id
  policy = data.aws_iam_policy_document.amplify_service.json
}

# --- Amplify アプリ ---
resource "aws_amplify_app" "main" {
  name     = local.name_prefix
  platform = "WEB_COMPUTE"
  # repository(GitHubリポジトリ)は Terraform では指定しない。
  # 指定するとアクセストークンが必須になり、トークンを保存しない GitHub App 連携と両立しないため。
  # repository と main ブランチはコンソールの GitHub App 連携で設定する(手順15)。

  # SSR 実行ロール(BFF が実行時に Secrets Manager を読むため)
  compute_role_arn = aws_iam_role.amplify_compute.arn
  # サービスロール(ビルド・デプロイ、CloudWatch Logs への出力。4.9)
  iam_service_role_arn = aws_iam_role.amplify_service.arn

  # 秘密情報そのものは置かない(名前・URLのみ)。SESSION_SECRET 等は実行時にロールで読む
  environment_variables = {
    API_BASE_URL  = "${aws_apigatewayv2_api.http.api_endpoint}/api"
    BFF_SECRET_ID = aws_secretsmanager_secret.bff.name
    # モノレポのアプリルート。GitHub App 連携時に Amplify が設定する値を Terraform でも明示し、
    # apply で消えて frontend 配下がビルドされなくなるのを防ぐ。
    AMPLIFY_MONOREPO_APP_ROOT = "frontend"
  }

  # ビルド設定はリポジトリルートの amplify.yml を使う(モノレポ・Node.js 24。aws-architecture.md 4.7・12章)

  # GitHub App 連携(コンソール)で後から設定される repository・トークンを Terraform の差分対象から外す。
  # custom_rule は Amplify がフレームワーク検出で自動追加するため、Terraform で上書きしない。
  lifecycle {
    ignore_changes = [repository, access_token, oauth_token, custom_rule]
  }

  tags = { Name = local.name_prefix }
}

# --- main ブランチ ---
# main ブランチはコンソールの GitHub App 連携時に作成される(手順15)。
# 連携前に Terraform でブランチだけ作ることはできないため、ここでは管理しない。

# --- カスタムドメイン(tms.accent24.jp)。証明書は Amplify(ACM)が管理 ---
# DNS はムームーDNSで手動設定するため、検証完了を待たない(wait_for_verification=false)。
# GitHub App 連携(手順15)で main ブランチが作成された後に apply し、出力された CNAME をムームーDNSに追加する(手順16)。
resource "aws_amplify_domain_association" "main" {
  app_id                = aws_amplify_app.main.id
  domain_name           = var.app_domain
  wait_for_verification = false

  sub_domain {
    # GitHub App 連携で作成される main ブランチを指す
    branch_name = "main"
    prefix      = ""
  }
}
