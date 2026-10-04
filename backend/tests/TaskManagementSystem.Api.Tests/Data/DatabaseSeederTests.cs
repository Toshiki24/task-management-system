using Npgsql;
using TaskManagementSystem.Api.Data;

namespace TaskManagementSystem.Api.Tests.Data;

// DatabaseSeeder を実際の PostgreSQL に対して検証する。
// 複数の初期ユーザーがアプリ用ユーザー(tms_app・DML権限)で作成されること・パスワードが
// BCrypt で保存されること・再実行しても重複しないこと(冪等)を確認する。
public class DatabaseSeederTests
{
    private const string ServerConnectionEnvName = "TEST_DB_CONNECTION";
    private const string DefaultServerConnection =
        "Host=localhost;Port=5432;Username=postgres;Password=postgres";

    private static string ServerConnection =>
        Environment.GetEnvironmentVariable(ServerConnectionEnvName) ?? DefaultServerConnection;

    [Fact(DisplayName = "UT-1301 複数の初期ユーザーの作成・パスワードのハッシュ化・冪等性")]
    public async Task SeedUsersAsync_CreatesUsersWithHashedPasswordsAndIsIdempotent()
    {
        var suffix = Guid.NewGuid().ToString("N")[..12];
        var database = $"task_management_seed_{suffix}";
        var appUser = $"tms_app_{suffix}";
        const string appPassword = "app-password-1234";

        var users = new List<SeedUser>
        {
            new("管理者ユーザー", "admin@example.com", "Password123!"),
            new("山田太郎", "yamada@example.com", "Password123!"),
            new("鈴木花子", "suzuki@example.com", "Password123!"),
        };

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
            // スキーマ作成＋アプリ用ユーザー(tms_app)の作成・権限付与
            await DatabaseMigrator.RunAsync(masterConnection, appConnection);

            // 1回目: アプリ用ユーザーで3件作成される(INSERT は DML 権限で足りる)
            var created = await DatabaseSeeder.SeedUsersAsync(appConnection, users);
            Assert.Equal(3, created);

            // 保存されたハッシュが BCrypt で、元パスワードと一致すること(平文は保存しない)
            await using (var db = new NpgsqlConnection(masterConnection))
            {
                await db.OpenAsync();
                await using var cmd = new NpgsqlCommand(
                    "SELECT password_hash FROM users WHERE email = @email", db);
                cmd.Parameters.AddWithValue("email", "admin@example.com");
                var hash = (string)(await cmd.ExecuteScalarAsync())!;

                Assert.StartsWith("$2", hash);
                Assert.NotEqual("Password123!", hash);
                Assert.True(BCrypt.Net.BCrypt.Verify("Password123!", hash));
            }

            // 2回目: 既に存在するので作成されない(冪等)。総数も3件のまま
            var createdAgain = await DatabaseSeeder.SeedUsersAsync(appConnection, users);
            Assert.Equal(0, createdAgain);

            await using (var db = new NpgsqlConnection(masterConnection))
            {
                await db.OpenAsync();
                await using var count = new NpgsqlCommand("SELECT count(*) FROM users", db);
                Assert.Equal(3L, (long)(await count.ExecuteScalarAsync())!);
            }
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

    private static string AdminConnection() =>
        new NpgsqlConnectionStringBuilder(ServerConnection) { Database = "postgres" }.ConnectionString;
}
