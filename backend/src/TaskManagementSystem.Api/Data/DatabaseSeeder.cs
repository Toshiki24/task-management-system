using Npgsql;

namespace TaskManagementSystem.Api.Data;

/// <summary>
/// 初期管理者ユーザーの投入。登録APIが無いため、本番の初回ログイン用ユーザーを
/// マイグレーション用 Lambda から冪等に作成する(aws-architecture.md 4.4)。
/// </summary>
public static class DatabaseSeeder
{
    /// <summary>
    /// 指定したメールアドレスの管理者を作成する。既に存在する場合は何もしない(冪等)。
    /// パスワードはアプリと同じ BCrypt でハッシュ化して保存する(平文は保存しない)。
    /// 接続はアプリ用ユーザー(tms_app)で行う(INSERT は DML 権限で足りる)。
    /// </summary>
    /// <returns>新規作成したら true、既に存在していたら false。</returns>
    public static async Task<bool> SeedAdminAsync(
        string appConnectionString, string name, string email, string plainPassword)
    {
        var passwordHash = BCrypt.Net.BCrypt.HashPassword(plainPassword);

        await using var connection = new NpgsqlConnection(appConnectionString);
        await connection.OpenAsync();

        // email は一意制約(users_email_key)があるため、重複時は何もしない(冪等)
        await using var command = new NpgsqlCommand(
            "INSERT INTO users (name, email, password_hash) VALUES (@name, @email, @hash) "
                + "ON CONFLICT (email) DO NOTHING",
            connection);
        command.Parameters.AddWithValue("name", name);
        command.Parameters.AddWithValue("email", email);
        command.Parameters.AddWithValue("hash", passwordHash);

        var affected = await command.ExecuteNonQueryAsync();
        return affected > 0;
    }
}
