using TaskManagementSystem.Api.Data;

namespace TaskManagementSystem.Api.Configuration;

/// <summary>
/// 自己ホスト配布(Docker Compose 等、AWS 非依存。M5 §8)向けの起動時オプション。
/// AWS 本番ではマイグレーションは専用 Lambda(<see cref="MigrationBootstrap"/>)で適用するため、
/// 起動時マイグレーションは既定で無効。環境変数/設定 <c>RUN_MIGRATIONS_ON_STARTUP=true</c> で有効化する。
/// </summary>
public static class SelfHostStartup
{
    public const string RunMigrationsOnStartupKey = "RUN_MIGRATIONS_ON_STARTUP";
    public const string SeedAdminEmailKey = "SEED_ADMIN_EMAIL";
    public const string SeedAdminPasswordKey = "SEED_ADMIN_PASSWORD";
    public const string SeedAdminNameKey = "SEED_ADMIN_NAME";

    /// <summary>起動時にEFマイグレーションを適用するか。既定は false。</summary>
    public static bool ShouldRunMigrationsOnStartup(IConfiguration configuration)
    {
        return configuration.GetValue(RunMigrationsOnStartupKey, false);
    }

    /// <summary>
    /// 起動時に投入する初期 System Admin を設定から読み取る(新規インストールで最初の管理者を作るため)。
    /// <c>SEED_ADMIN_EMAIL</c> と <c>SEED_ADMIN_PASSWORD</c> の両方が指定されたときだけ返す。再実行は冪等。
    /// </summary>
    public static SeedUser? GetInitialAdmin(IConfiguration configuration)
    {
        var email = configuration[SeedAdminEmailKey];
        var password = configuration[SeedAdminPasswordKey];
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            return null;
        }

        var name = configuration[SeedAdminNameKey];
        if (string.IsNullOrWhiteSpace(name))
        {
            name = "System Admin";
        }

        return new SeedUser(name, email, password, IsSystemAdmin: true);
    }
}
