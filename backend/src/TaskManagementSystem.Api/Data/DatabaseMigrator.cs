using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace TaskManagementSystem.Api.Data;

/// <summary>
/// DB のマイグレーション(EF Core)と、アプリ用DBユーザー(最小権限)の作成・権限付与を行う。
/// マイグレーション用 Lambda から呼ばれる(aws-architecture.md 4.3・4.4)。
/// </summary>
/// <remarks>
/// マスターユーザーで接続し、(1)アプリ用ユーザーの作成/パスワード更新、(2)マイグレーションの適用、
/// (3)アプリ用ユーザーへの DML 権限の付与(SELECT/INSERT/UPDATE/DELETE のみ)を行う。
/// テーブルの所有者はマスターユーザーとし、アプリ用ユーザーには DDL を与えない(最小権限。4.3)。
/// </remarks>
public static class DatabaseMigrator
{
    public static async Task RunAsync(string masterConnectionString, string appConnectionString)
    {
        var appCsb = new NpgsqlConnectionStringBuilder(appConnectionString);
        var appUser = appCsb.Username
            ?? throw new InvalidOperationException("アプリ用接続文字列に Username がありません。");
        var appPassword = appCsb.Password
            ?? throw new InvalidOperationException("アプリ用接続文字列に Password がありません。");

        // 1. アプリ用ユーザーを作成(または存在すればパスワードを同期)
        await using (var connection = new NpgsqlConnection(masterConnectionString))
        {
            await connection.OpenAsync();
            await EnsureAppRoleAsync(connection, appUser, appPassword);
        }

        // 2. マイグレーションを適用(テーブルの所有者はマスターユーザー)
        await using (var context = CreateContext(masterConnectionString))
        {
            await context.Database.MigrateAsync();
        }

        // 3. アプリ用ユーザーに DML 権限を付与
        await using (var connection = new NpgsqlConnection(masterConnectionString))
        {
            await connection.OpenAsync();
            await GrantAppPrivilegesAsync(connection, appUser);
        }
    }

    private static AppDbContext CreateContext(string connectionString) =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(connectionString)
            .UseSnakeCaseNamingConvention()
            .Options);

    private static async Task EnsureAppRoleAsync(NpgsqlConnection connection, string appUser, string appPassword)
    {
        bool exists;
        await using (var check = new NpgsqlCommand("SELECT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = @u)", connection))
        {
            check.Parameters.AddWithValue("u", appUser);
            exists = (bool)(await check.ExecuteScalarAsync())!;
        }

        // 識別子・パスワードはサーバー側の format(%I/%L)で安全にエスケープしてから実行する(SQLインジェクション防止)
        var verb = exists ? "ALTER" : "CREATE";
        string statement;
        await using (var build = new NpgsqlCommand(
            $"SELECT format('{verb} ROLE %I WITH LOGIN PASSWORD %L', @u, @p)", connection))
        {
            build.Parameters.AddWithValue("u", appUser);
            build.Parameters.AddWithValue("p", appPassword);
            statement = (string)(await build.ExecuteScalarAsync())!;
        }

        await using var exec = new NpgsqlCommand(statement, connection);
        await exec.ExecuteNonQueryAsync();
    }

    private static async Task GrantAppPrivilegesAsync(NpgsqlConnection connection, string appUser)
    {
        // ユーザー識別子を安全にクオートする
        string quotedUser;
        await using (var q = new NpgsqlCommand("SELECT quote_ident(@u)", connection))
        {
            q.Parameters.AddWithValue("u", appUser);
            quotedUser = (string)(await q.ExecuteScalarAsync())!;
        }

        // 参照・更新のみ(DDLは与えない)。既存テーブル/シーケンスと、今後作られるもの(default privileges)の両方に付与
        var statements = new[]
        {
            $"GRANT CONNECT ON DATABASE {QuoteIdent(connection)} TO {quotedUser}",
            $"GRANT USAGE ON SCHEMA public TO {quotedUser}",
            $"GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA public TO {quotedUser}",
            $"GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA public TO {quotedUser}",
            $"ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO {quotedUser}",
            $"ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT USAGE, SELECT ON SEQUENCES TO {quotedUser}",
        };

        foreach (var statement in statements)
        {
            await using var exec = new NpgsqlCommand(statement, connection);
            await exec.ExecuteNonQueryAsync();
        }
    }

    // 接続中のデータベース名を安全にクオートして返す
    private static string QuoteIdent(NpgsqlConnection connection)
    {
        using var cmd = new NpgsqlCommand("SELECT quote_ident(current_database())", connection);
        return (string)cmd.ExecuteScalar()!;
    }
}
