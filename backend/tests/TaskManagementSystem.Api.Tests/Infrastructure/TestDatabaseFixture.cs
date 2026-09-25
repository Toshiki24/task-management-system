using Microsoft.EntityFrameworkCore;
using Npgsql;
using TaskManagementSystem.Api.Data;

namespace TaskManagementSystem.Api.Tests.Infrastructure;

/// <summary>
/// テストクラスごとに使い捨てのPostgreSQLデータベースを作成・破棄する。
/// 開発用DB(task_management)には一切接続しないため、ローカルの動作確認データに影響しない。
/// また、テストクラス間でDBを共有しないため、並列実行されても互いのデータが干渉しない。
/// </summary>
public sealed class TestDatabaseFixture : IAsyncLifetime
{
    // 接続先サーバーは環境変数で上書きできる(データベース名は常にテスト専用の名前を使う)
    private const string ServerConnectionEnvName = "TEST_DB_CONNECTION";
    private const string DefaultServerConnection =
        "Host=localhost;Port=5432;Username=postgres;Password=postgres";

    private readonly string _databaseName = $"task_management_ut_{Guid.NewGuid():N}";
    private readonly string _serverConnectionString;

    public TestDatabaseFixture()
    {
        _serverConnectionString =
            Environment.GetEnvironmentVariable(ServerConnectionEnvName) ?? DefaultServerConnection;

        ConnectionString = new NpgsqlConnectionStringBuilder(_serverConnectionString)
        {
            Database = _databaseName,
        }.ConnectionString;
    }

    public string ConnectionString { get; }

    public AppDbContext CreateContext() => new(
        new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(ConnectionString)
            .UseSnakeCaseNamingConvention()
            .Options);

    public async Task InitializeAsync()
    {
        // Migrateはデータベースが存在しなければ作成してから、本番と同じMigrationを適用する
        await using var context = CreateContext();
        await context.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        // プールに残った接続があるとDROPできないため、先に解放してから削除する
        NpgsqlConnection.ClearAllPools();

        var adminConnectionString = new NpgsqlConnectionStringBuilder(_serverConnectionString)
        {
            Database = "postgres",
        }.ConnectionString;

        await using var connection = new NpgsqlConnection(adminConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            $"DROP DATABASE IF EXISTS \"{_databaseName}\" WITH (FORCE)", connection);
        await command.ExecuteNonQueryAsync();
    }
}
