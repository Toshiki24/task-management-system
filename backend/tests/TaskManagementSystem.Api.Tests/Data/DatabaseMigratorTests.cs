using Npgsql;
using TaskManagementSystem.Api.Data;

namespace TaskManagementSystem.Api.Tests.Data;

// DatabaseMigrator を実際の PostgreSQL に対して検証する。
// マイグレーションの適用と、アプリ用ユーザーが最小権限(DML のみ・DDL 不可)で作られることを確認する。
public class DatabaseMigratorTests
{
    private const string ServerConnectionEnvName = "TEST_DB_CONNECTION";
    private const string DefaultServerConnection =
        "Host=localhost;Port=5432;Username=postgres;Password=postgres";

    private static string ServerConnection =>
        Environment.GetEnvironmentVariable(ServerConnectionEnvName) ?? DefaultServerConnection;

    [Fact(DisplayName = "UT-1201 マイグレーション適用とアプリ用ユーザー(最小権限)の作成")]
    public async Task RunAsync_AppliesMigrationsAndCreatesLeastPrivilegeUser()
    {
        var suffix = Guid.NewGuid().ToString("N")[..12];
        var database = $"task_management_mig_{suffix}";
        var appUser = $"tms_app_{suffix}";
        const string appPassword = "app-password-1234";

        var masterConnection = new NpgsqlConnectionStringBuilder(ServerConnection) { Database = database }.ConnectionString;
        var appConnection = new NpgsqlConnectionStringBuilder(ServerConnection)
        {
            Database = database,
            Username = appUser,
            Password = appPassword,
        }.ConnectionString;

        await CreateDatabaseAsync(database);
        try
        {
            await DatabaseMigrator.RunAsync(masterConnection, appConnection);

            // テーブルが作成されていること(users はアプリの主要テーブル)
            await using (var master = new NpgsqlConnection(masterConnection))
            {
                await master.OpenAsync();
                await using var cmd = new NpgsqlCommand(
                    "SELECT to_regclass('public.users') IS NOT NULL", master);
                Assert.True((bool)(await cmd.ExecuteScalarAsync())!);
            }

            // アプリ用ユーザーで接続でき、参照できること(DMLは許可)
            await using (var app = new NpgsqlConnection(appConnection))
            {
                await app.OpenAsync();
                await using var select = new NpgsqlCommand("SELECT count(*) FROM users", app);
                Assert.Equal(0L, (long)(await select.ExecuteScalarAsync())!);

                // DDL(テーブル作成)は拒否されること(最小権限)
                await using var ddl = new NpgsqlCommand("CREATE TABLE should_fail (id int)", app);
                await Assert.ThrowsAsync<PostgresException>(() => ddl.ExecuteNonQueryAsync());
            }

            // 再実行しても失敗しないこと(冪等)
            await DatabaseMigrator.RunAsync(masterConnection, appConnection);
        }
        finally
        {
            await DropDatabaseAsync(database);
            await DropRoleAsync(appUser);
        }
    }

    private static async Task CreateDatabaseAsync(string database)
    {
        await using var admin = new NpgsqlConnection(AdminConnection());
        await admin.OpenAsync();
        await using var cmd = new NpgsqlCommand($"CREATE DATABASE \"{database}\"", admin);
        await cmd.ExecuteNonQueryAsync();
    }

    private static async Task DropDatabaseAsync(string database)
    {
        await using var admin = new NpgsqlConnection(AdminConnection());
        await admin.OpenAsync();
        await using var cmd = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{database}\" WITH (FORCE)", admin);
        await cmd.ExecuteNonQueryAsync();
    }

    private static async Task DropRoleAsync(string role)
    {
        await using var admin = new NpgsqlConnection(AdminConnection());
        await admin.OpenAsync();
        await using var cmd = new NpgsqlCommand($"DROP ROLE IF EXISTS \"{role}\"", admin);
        await cmd.ExecuteNonQueryAsync();
    }

    // データベースの作成・削除は postgres データベースに接続して行う
    private static string AdminConnection() =>
        new NpgsqlConnectionStringBuilder(ServerConnection) { Database = "postgres" }.ConnectionString;
}
