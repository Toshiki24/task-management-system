# ECR(コンテナイメージのリポジトリ)。設計: aws-architecture.md 4.6。
resource "aws_ecr_repository" "api" {
  name = "${var.project_name}-api"
  # デプロイしたイメージを後から差し替えられないようにする(コミットハッシュをタグにする)
  image_tag_mutability = "IMMUTABLE"

  # プッシュ時に基本スキャンを実行(無料)
  image_scanning_configuration {
    scan_on_push = true
  }

  tags = { Name = "${var.project_name}-api" }
}

# 最新の10個を残して古いイメージを削除する(保存容量の課金を抑える。ロールバック用に直近は残す)
resource "aws_ecr_lifecycle_policy" "api" {
  repository = aws_ecr_repository.api.name
  policy = jsonencode({
    rules = [
      {
        rulePriority = 1
        description  = "最新の10個を残して削除"
        selection = {
          tagStatus   = "any"
          countType   = "imageCountMoreThan"
          countNumber = 10
        }
        action = { type = "expire" }
      }
    ]
  })
}
