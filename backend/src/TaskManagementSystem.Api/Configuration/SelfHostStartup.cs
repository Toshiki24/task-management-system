namespace TaskManagementSystem.Api.Configuration;

/// <summary>
/// 自己ホスト配布(Docker Compose 等、AWS 非依存。M5 §8)向けの起動時オプション。
/// AWS 本番ではマイグレーションは専用 Lambda(<see cref="MigrationBootstrap"/>)で適用するため、
/// 起動時マイグレーションは既定で無効。環境変数/設定 <c>RUN_MIGRATIONS_ON_STARTUP=true</c> で有効化する。
/// 初回管理者はセットアップ画面(<c>SELF_HOST_SETUP=true</c>)から作成する(env での決め打ちはしない)。
/// </summary>
public static class SelfHostStartup
{
    public const string RunMigrationsOnStartupKey = "RUN_MIGRATIONS_ON_STARTUP";
    public const string SetupEnabledKey = "SELF_HOST_SETUP";

    /// <summary>起動時にEFマイグレーションを適用するか。既定は false。</summary>
    public static bool ShouldRunMigrationsOnStartup(IConfiguration configuration)
    {
        return configuration.GetValue(RunMigrationsOnStartupKey, false);
    }

    /// <summary>
    /// 初回セットアップ(初回管理者作成)機能を有効にするか。既定は false。
    /// AWS 本番ではこのフラグを設定しないため、セットアップ用エンドポイントは無効(404)になる。
    /// </summary>
    public static bool IsSetupEnabled(IConfiguration configuration)
    {
        return configuration.GetValue(SetupEnabledKey, false);
    }
}
