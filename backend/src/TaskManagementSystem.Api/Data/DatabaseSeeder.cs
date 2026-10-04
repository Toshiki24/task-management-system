using Npgsql;

namespace TaskManagementSystem.Api.Data;

/// <summary>投入するユーザー1件分の情報。</summary>
public sealed record SeedUser(string Name, string Email, string Password);

/// <summary>
/// 初期ユーザーの投入。登録APIが無いため、本番の初回ログイン用ユーザーを
/// マイグレーション用 Lambda から冪等に作成する(aws-architecture.md 4.4)。
/// </summary>
public static class DatabaseSeeder
{
    /// <summary>
    /// 指定したユーザーを作成する。既に同じメールアドレスが存在する場合はスキップする(冪等)。
    /// パスワードはアプリと同じ BCrypt でハッシュ化して保存する(平文は保存しない)。
    /// 接続はアプリ用ユーザー(tms_app)で行う(INSERT は DML 権限で足りる)。
    /// </summary>
    /// <returns>新規作成した件数。</returns>
    public static async Task<int> SeedUsersAsync(string appConnectionString, IReadOnlyList<SeedUser> users)
    {
        if (users.Count == 0)
        {
            return 0;
        }

        await using var connection = new NpgsqlConnection(appConnectionString);
        await connection.OpenAsync();

        var created = 0;
        foreach (var user in users)
        {
            // email は一意制約(users_email_key)があるため、重複時は何もしない(冪等)
            await using var command = new NpgsqlCommand(
                "INSERT INTO users (name, email, password_hash) VALUES (@name, @email, @hash) "
                    + "ON CONFLICT (email) DO NOTHING",
                connection);
            command.Parameters.AddWithValue("name", user.Name);
            command.Parameters.AddWithValue("email", user.Email);
            command.Parameters.AddWithValue("hash", BCrypt.Net.BCrypt.HashPassword(user.Password));
            created += await command.ExecuteNonQueryAsync();
        }

        return created;
    }
}
