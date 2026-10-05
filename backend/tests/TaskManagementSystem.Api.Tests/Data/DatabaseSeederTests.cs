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

    [Fact(DisplayName = "UT-1302 初期セットアップ: 既定ワークスペース作成・System Admin 付与・冪等")]
    public async Task SetupInitialOrganizationAsync_CreatesWorkspaceAndSystemAdmin()
    {
        var suffix = Guid.NewGuid().ToString("N")[..12];
        var database = $"task_management_setup_{suffix}";
        var appUser = $"tms_app_{suffix}";
        const string appPassword = "app-password-1234";

        var users = new List<SeedUser>
        {
            new("管理者", "admin@example.com", "Password123!", IsSystemAdmin: true),
            new("一般ユーザー", "member@example.com", "Password123!"),
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
            await DatabaseMigrator.RunAsync(masterConnection, appConnection);
            await DatabaseSeeder.SeedUsersAsync(appConnection, users);

            var workspaceId = await DatabaseSeeder.SetupInitialOrganizationAsync(appConnection, users);

            await using var db = new NpgsqlConnection(masterConnection);
            await db.OpenAsync();

            // 既定ワークスペースが1つだけ作られる
            await using (var count = new NpgsqlCommand("SELECT count(*) FROM workspaces", db))
            {
                Assert.Equal(1L, (long)(await count.ExecuteScalarAsync())!);
            }

            // 管理者は System Admin かつ ADMIN、一般ユーザーは MEMBER
            Assert.True(await IsSystemAdminAsync(db, "admin@example.com"));
            Assert.False(await IsSystemAdminAsync(db, "member@example.com"));
            Assert.Equal("ADMIN", await MemberRoleAsync(db, workspaceId, "admin@example.com"));
            Assert.Equal("MEMBER", await MemberRoleAsync(db, workspaceId, "member@example.com"));

            // 再実行しても冪等(ワークスペースは増えず、ロールも変わらない)
            var secondWorkspaceId = await DatabaseSeeder.SetupInitialOrganizationAsync(appConnection, users);
            Assert.Equal(workspaceId, secondWorkspaceId);
            await using (var count = new NpgsqlCommand("SELECT count(*) FROM workspaces", db))
            {
                Assert.Equal(1L, (long)(await count.ExecuteScalarAsync())!);
            }
            await using (var members = new NpgsqlCommand(
                "SELECT count(*) FROM workspace_members WHERE workspace_id = @ws", db))
            {
                members.Parameters.AddWithValue("ws", workspaceId);
                Assert.Equal(2L, (long)(await members.ExecuteScalarAsync())!);
            }
        }
        finally
        {
            await DropDatabaseAsync(database);
            await DropRoleAsync(appUser);
        }
    }

    private static async Task<bool> IsSystemAdminAsync(NpgsqlConnection db, string email)
    {
        await using var cmd = new NpgsqlCommand("SELECT is_system_admin FROM users WHERE email = @email", db);
        cmd.Parameters.AddWithValue("email", email);
        return (bool)(await cmd.ExecuteScalarAsync())!;
    }

    private static async Task<string> MemberRoleAsync(NpgsqlConnection db, long workspaceId, string email)
    {
        await using var cmd = new NpgsqlCommand(
            "SELECT wm.role FROM workspace_members wm JOIN users u ON u.id = wm.user_id "
                + "WHERE wm.workspace_id = @ws AND u.email = @email", db);
        cmd.Parameters.AddWithValue("ws", workspaceId);
        cmd.Parameters.AddWithValue("email", email);
        return (string)(await cmd.ExecuteScalarAsync())!;
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
