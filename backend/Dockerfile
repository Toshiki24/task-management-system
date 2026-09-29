# バックエンドAPIを AWS Lambda(コンテナイメージ)で動かすためのイメージ。
# ビルド: backend ディレクトリで `docker build -t task-management-api .`
#
# Lambda では Amazon.Lambda.AspNetCoreServer.Hosting(Program.cs の AddAWSLambdaHosting)が
# API Gateway(HTTP API)からのイベントを ASP.NET Core のリクエストに変換する。

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# 依存パッケージの復元を先に行い、ソースだけを変更した場合はキャッシュを使えるようにする
COPY src/TaskManagementSystem.Api/TaskManagementSystem.Api.csproj src/TaskManagementSystem.Api/
RUN dotnet restore src/TaskManagementSystem.Api/TaskManagementSystem.Api.csproj

COPY src/TaskManagementSystem.Api/ src/TaskManagementSystem.Api/
RUN dotnet publish src/TaskManagementSystem.Api/TaskManagementSystem.Api.csproj \
    --configuration Release \
    --no-restore \
    --output /app/publish \
    -p:UseAppHost=false

# AWS が提供する .NET 8 の Lambda 用ベースイメージ(Lambda の実行環境と同じ構成)
FROM public.ecr.aws/lambda/dotnet:8
COPY --from=build /app/publish ${LAMBDA_TASK_ROOT}

# 実行可能アセンブリ(トップレベルステートメント)を Lambda で動かす場合、ハンドラーはアセンブリ名を指定する
CMD ["TaskManagementSystem.Api"]
