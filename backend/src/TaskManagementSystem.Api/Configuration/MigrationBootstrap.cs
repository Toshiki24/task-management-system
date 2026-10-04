using System.Text.Json;
using Amazon.Lambda.Core;
using Amazon.Lambda.RuntimeSupport;
using Amazon.Lambda.Serialization.SystemTextJson;
using Amazon.SecretsManager;
using Amazon.SecretsManager.Model;
using Npgsql;
using TaskManagementSystem.Api.Data;

namespace TaskManagementSystem.Api.Configuration;

/// <summary>
/// マイグレーション用 Lambda の起動処理。環境変数 <c>MIGRATION_MODE=true</c> のときに、
/// 通常のAPIホストの代わりにこの処理を実行する(同じコンテナイメージを使う。aws-architecture.md 4.4)。
/// </summary>
/// <remarks>
/// アプリ用接続文字列(<c>ConnectionStrings:DefaultConnection</c>、APP_SECRET_ID 由来)のホスト等を使い、
/// 認証情報だけを RDS マスターユーザーのシークレット(環境変数 <c>RDS_MASTER_SECRET_ID</c>)に差し替えて
/// マスター接続を作り、<see cref="DatabaseMigrator"/> を実行する。
/// </remarks>
public static class MigrationBootstrap
{
    public const string MigrationModeEnvironmentVariable = "MIGRATION_MODE";
    public const string MasterSecretIdEnvironmentVariable = "RDS_MASTER_SECRET_ID";

    public static bool IsMigrationMode =>
        Environment.GetEnvironmentVariable(MigrationModeEnvironmentVariable) == "true";

    public static async Task RunAsync(IConfiguration configuration)
    {
        var appConnectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection が設定されていません。");
        var masterSecretId = Environment.GetEnvironmentVariable(MasterSecretIdEnvironmentVariable)
            ?? throw new InvalidOperationException($"環境変数 {MasterSecretIdEnvironmentVariable} が設定されていません。");

        var masterConnectionString = await BuildMasterConnectionStringAsync(appConnectionString, masterSecretId);

        // Lambda から呼ばれるたびにマイグレーションを実行する(EF のマイグレーションは再実行しても安全)。
        // 入力(任意)に管理者情報が含まれていれば、初期管理者を冪等に作成する(登録APIが無いため)。
        // マイグレーションのみ実行する場合は空オブジェクト {} を渡す。
        var handler = async (MigrationRequest? request, ILambdaContext context) =>
        {
            await DatabaseMigrator.RunAsync(masterConnectionString, appConnectionString);
            context.Logger.LogInformation("マイグレーションとアプリ用ユーザーの作成が完了しました。");

            if (request is { AdminEmail: { Length: > 0 }, AdminPassword: { Length: > 0 } })
            {
                var name = string.IsNullOrWhiteSpace(request.AdminName) ? "管理者" : request.AdminName;
                var created = await DatabaseSeeder.SeedAdminAsync(
                    appConnectionString, name, request.AdminEmail, request.AdminPassword);
                context.Logger.LogInformation(
                    created
                        ? $"初期管理者を作成しました: {request.AdminEmail}"
                        : $"初期管理者は既に存在します: {request.AdminEmail}");
                return created ? "migration completed; admin created" : "migration completed; admin already exists";
            }

            return "migration completed";
        };

        using var wrapper = HandlerWrapper.GetHandlerWrapper(handler, new DefaultLambdaJsonSerializer());
        using var bootstrap = new LambdaBootstrap(wrapper);
        await bootstrap.RunAsync();
    }

    // アプリ用接続文字列(ホスト・ポート・DB名・SSL設定)を流用し、認証情報だけマスターのものに差し替える
    private static async Task<string> BuildMasterConnectionStringAsync(string appConnectionString, string masterSecretId)
    {
        using var client = new AmazonSecretsManagerClient();
        var response = await client.GetSecretValueAsync(new GetSecretValueRequest { SecretId = masterSecretId });
        using var document = JsonDocument.Parse(response.SecretString);
        var root = document.RootElement;

        var builder = new NpgsqlConnectionStringBuilder(appConnectionString)
        {
            Username = root.GetProperty("username").GetString(),
            Password = root.GetProperty("password").GetString(),
        };
        return builder.ConnectionString;
    }
}

/// <summary>
/// マイグレーション用 Lambda の入力。任意で初期管理者の情報を受け取る。
/// 全て未指定(空オブジェクト {})ならマイグレーションのみ実行する。
/// </summary>
public sealed record MigrationRequest(string? AdminName, string? AdminEmail, string? AdminPassword);
