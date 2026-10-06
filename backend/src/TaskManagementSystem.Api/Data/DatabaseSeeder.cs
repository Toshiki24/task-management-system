using Npgsql;

namespace TaskManagementSystem.Api.Data;

/// <summary>
/// 投入するユーザー1件分の情報。<paramref name="IsSystemAdmin"/>=true のユーザーは
/// 初期セットアップで System Admin かつ既定ワークスペースの Admin になる(Phase 2 M1 §8)。
/// </summary>
public sealed record SeedUser(string Name, string Email, string Password, bool IsSystemAdmin = false);

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

    /// <summary>
    /// 初期セットアップ(Phase 2 M1 §8)。既定ワークスペースを用意し、指定ユーザーを所属させる。
    /// <see cref="SeedUser.IsSystemAdmin"/>=true のユーザーは System Admin かつ既定ワークスペースの Admin にする。
    /// 新規インストールでも既存環境でも安全に再実行できる(冪等)。
    /// </summary>
    /// <returns>既定ワークスペースの ID。</returns>
    public static async Task<long> SetupInitialOrganizationAsync(
        string appConnectionString,
        IReadOnlyList<SeedUser> users,
        string defaultWorkspaceName = "Default Workspace")
    {
        await using var connection = new NpgsqlConnection(appConnectionString);
        await connection.OpenAsync();

        // 1. 既定ワークスペースを get-or-create(名前で同定。既に存在すればそれを使う)
        long workspaceId;
        await using (var command = new NpgsqlCommand(
            "WITH existing AS (SELECT id FROM workspaces WHERE name = @name ORDER BY id LIMIT 1), "
                + "inserted AS ("
                + "  INSERT INTO workspaces (name, description) "
                + "  SELECT @name, @desc WHERE NOT EXISTS (SELECT 1 FROM existing) RETURNING id) "
                + "SELECT id FROM existing UNION ALL SELECT id FROM inserted LIMIT 1",
            connection))
        {
            command.Parameters.AddWithValue("name", defaultWorkspaceName);
            command.Parameters.AddWithValue("desc", "初期セットアップで作成された既定のワークスペース");
            workspaceId = (long)(await command.ExecuteScalarAsync())!;
        }

        // 1.5 既定ワークフロー(TODO/IN_PROGRESS/DONE)を用意する(Phase 2 M2 §3.1)。
        //     新規インストールでは既定ワークスペースがこのセットアップで初めて作られるため、
        //     マイグレーションのバックフィル対象にならない。状態が無い場合のみ投入する(冪等)。
        await using (var command = new NpgsqlCommand(
            "INSERT INTO workflow_states (workspace_id, key, name, category, position, is_default) "
                + "SELECT @ws, v.key, v.name, v.category, v.position, v.is_default "
                + "FROM (VALUES "
                + "  ('TODO', '未着手', 'TODO', 0, true), "
                + "  ('IN_PROGRESS', '対応中', 'IN_PROGRESS', 1, false), "
                + "  ('DONE', '完了', 'DONE', 2, false) "
                + ") AS v(key, name, category, position, is_default) "
                + "WHERE NOT EXISTS (SELECT 1 FROM workflow_states ws WHERE ws.workspace_id = @ws)",
            connection))
        {
            command.Parameters.AddWithValue("ws", workspaceId);
            await command.ExecuteNonQueryAsync();
        }

        // 2. ユーザーごとに System Admin フラグとワークスペース所属を設定する
        foreach (var user in users)
        {
            if (user.IsSystemAdmin)
            {
                await using var flag = new NpgsqlCommand(
                    "UPDATE users SET is_system_admin = true WHERE email = @email", connection);
                flag.Parameters.AddWithValue("email", user.Email);
                await flag.ExecuteNonQueryAsync();
            }

            // System Admin は WS Admin に(既に MEMBER でも昇格)、それ以外は MEMBER として追加(既存は維持)
            var role = user.IsSystemAdmin ? "ADMIN" : "MEMBER";
            var conflictAction = user.IsSystemAdmin ? "DO UPDATE SET role = @role" : "DO NOTHING";
            await using var member = new NpgsqlCommand(
                "INSERT INTO workspace_members (workspace_id, user_id, role) "
                    + "SELECT @ws, id, @role FROM users WHERE email = @email "
                    + $"ON CONFLICT (workspace_id, user_id) {conflictAction}",
                connection);
            member.Parameters.AddWithValue("ws", workspaceId);
            member.Parameters.AddWithValue("role", role);
            member.Parameters.AddWithValue("email", user.Email);
            await member.ExecuteNonQueryAsync();
        }

        return workspaceId;
    }
}
