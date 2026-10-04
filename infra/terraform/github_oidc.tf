# GitHub Actions 用の OIDC ロール。設計: aws-architecture.md 4.9、security-review-2.md SEC2-05。
# GitHub の OIDC で一時的な認証情報を取得し、ECR へのイメージのプッシュと Lambda のイメージ更新のみを許可する。
# アクセスキーは GitHub に保存しない。信頼は本リポジトリの main ブランチからの実行に限定する(sub 完全一致)。

# Lambda のイメージ更新対象(スライス6で同名のLambdaを作成する)。存在前でもARNで指定できる
locals {
  api_lambda_arn       = "arn:aws:lambda:${var.aws_region}:${data.aws_caller_identity.current.account_id}:function:${local.name_prefix}-api"
  migration_lambda_arn = "arn:aws:lambda:${var.aws_region}:${data.aws_caller_identity.current.account_id}:function:${local.name_prefix}-migration"
}

resource "aws_iam_openid_connect_provider" "github" {
  url            = "https://token.actions.githubusercontent.com"
  client_id_list = ["sts.amazonaws.com"]
  # thumbprint_list は省略し、AWS に信頼ストアでの検証を任せる(GitHub の証明書ローテーションの影響を受けない)
}

# 信頼ポリシー: aud=sts.amazonaws.com かつ sub が「本リポジトリの main」に完全一致する場合のみ AssumeRole を許可
data "aws_iam_policy_document" "github_actions_assume" {
  statement {
    effect  = "Allow"
    actions = ["sts:AssumeRoleWithWebIdentity"]

    principals {
      type        = "Federated"
      identifiers = [aws_iam_openid_connect_provider.github.arn]
    }

    condition {
      test     = "StringEquals"
      variable = "token.actions.githubusercontent.com:aud"
      values   = ["sts.amazonaws.com"]
    }

    # 本リポジトリの main ブランチからの実行のみに限定する(SEC2-05)。fork や他ブランチからは assume できない。
    # このアカウントの OIDC は subject(sub)に owner/repo の数値IDが埋め込まれる構成のため、
    # sub ではなく安定した repository と ref のクレームで完全一致を取る。
    # (repository と ref の両方を満たす場合のみ許可＝このリポジトリかつ main ブランチ)
    condition {
      test     = "StringEquals"
      variable = "token.actions.githubusercontent.com:repository"
      values   = [var.github_repository]
    }

    condition {
      test     = "StringEquals"
      variable = "token.actions.githubusercontent.com:ref"
      values   = ["refs/heads/main"]
    }
  }
}

resource "aws_iam_role" "github_actions" {
  name               = "${local.name_prefix}-github-actions"
  assume_role_policy = data.aws_iam_policy_document.github_actions_assume.json
  tags               = { Name = "${local.name_prefix}-github-actions" }
}

# 許可する操作: ECR へのプッシュ(対象リポジトリのみ)と、API用・マイグレーション用 Lambda のイメージ更新のみ
data "aws_iam_policy_document" "github_actions" {
  # ECR の認証トークン取得(リソース指定不可のため * が必要)
  statement {
    sid       = "EcrAuthToken"
    effect    = "Allow"
    actions   = ["ecr:GetAuthorizationToken"]
    resources = ["*"]
  }

  # 対象リポジトリへのイメージのプッシュ
  statement {
    sid    = "EcrPush"
    effect = "Allow"
    actions = [
      "ecr:BatchCheckLayerAvailability",
      "ecr:InitiateLayerUpload",
      "ecr:UploadLayerPart",
      "ecr:CompleteLayerUpload",
      "ecr:PutImage",
      "ecr:BatchGetImage",
      "ecr:GetDownloadUrlForLayer",
    ]
    resources = [aws_ecr_repository.api.arn]
  }

  # API用・マイグレーション用 Lambda のイメージ更新のみ
  statement {
    sid       = "LambdaUpdateImage"
    effect    = "Allow"
    actions   = ["lambda:UpdateFunctionCode", "lambda:GetFunction"]
    resources = [local.api_lambda_arn, local.migration_lambda_arn]
  }
}

resource "aws_iam_role_policy" "github_actions" {
  name   = "${local.name_prefix}-github-actions"
  role   = aws_iam_role.github_actions.id
  policy = data.aws_iam_policy_document.github_actions.json
}
