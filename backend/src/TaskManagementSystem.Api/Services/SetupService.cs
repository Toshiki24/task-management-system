using System.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using TaskManagementSystem.Api.Configuration;
using TaskManagementSystem.Api.Data;
using TaskManagementSystem.Api.Dtos.Setup;
using TaskManagementSystem.Api.Models;

namespace TaskManagementSystem.Api.Services;

/// <summary>
/// 初回セットアップ(初回 System Admin 作成。M5 §8)。
/// セルフホストで <c>SELF_HOST_SETUP=true</c> のときだけ有効。ユーザーが1人も居ないときに限り、
/// SERIALIZABLE トランザクションで原子的に最初の管理者を作成する(同時実行でも二重作成しない)。
/// </summary>
public class SetupService : ISetupService
{
    // PostgreSQL のエラーコード
    private const string SerializationFailure = "40001";
    private const string UniqueViolation = "23505";

    private readonly AppDbContext _dbContext;
    private readonly IConfiguration _configuration;
    private readonly ILogger<SetupService> _logger;

    public SetupService(AppDbContext dbContext, IConfiguration configuration, ILogger<SetupService> logger)
    {
        _dbContext = dbContext;
        _configuration = configuration;
        _logger = logger;
    }

    private bool IsEnabled => SelfHostStartup.IsSetupEnabled(_configuration);

    public async Task<SetupStatusOutcome> GetStatusAsync()
    {
        if (!IsEnabled)
        {
            return new SetupStatusOutcome(SetupStatusResult.Disabled);
        }

        try
        {
            var hasAnyUser = await _dbContext.Users.AnyAsync();
            return new SetupStatusOutcome(SetupStatusResult.Ok, new SetupStatusDto(!hasAnyUser));
        }
        catch (Exception ex)
        {
            // DB に接続できない等で状態を確認できない場合は、未セットアップと誤認させない(安全側)。
            _logger.LogError(ex, "初回セットアップ状態の確認に失敗しました。");
            return new SetupStatusOutcome(SetupStatusResult.DbError);
        }
    }

    public async Task<CreateInitialAdminResult> CreateInitialAdminAsync(CreateInitialAdminRequest request)
    {
        if (!IsEnabled)
        {
            return CreateInitialAdminResult.Disabled;
        }

        try
        {
            // 件数チェックだけに依存せず、SERIALIZABLE で「0件確認→作成」を原子的に行う。
            // 同時実行時は PostgreSQL の直列化検査が一方を 40001 で失敗させる。
            await using (var transaction =
                await _dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable))
            {
                if (await _dbContext.Users.AnyAsync())
                {
                    // トランザクションは破棄時にロールバックされる
                    return CreateInitialAdminResult.AlreadyInitialized;
                }

                _dbContext.Users.Add(new User
                {
                    Name = request.Name,
                    Email = request.Email,
                    // アプリ・シーダと同じ BCrypt でハッシュ化する(平文は保存しない)
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
                    IsSystemAdmin = true,
                });
                await _dbContext.SaveChangesAsync();
                await transaction.CommitAsync();
            }
        }
        catch (Exception ex) when (IsConflict(ex))
        {
            // 同時実行での競合、または同一メールの二重投入 → 既に初期化済みとして扱う
            return CreateInitialAdminResult.AlreadyInitialized;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "初回管理者の作成に失敗しました。");
            return CreateInitialAdminResult.DbError;
        }

        // 既定ワークスペース・ワークフロー・WS Admin 所属を冪等に用意する(M1 §8 の初期セットアップを再利用)。
        // 管理者レコードは作成済みのため、ここが失敗してもログイン自体は可能(ベストエフォート)。
        try
        {
            var connectionString = _configuration.GetConnectionString("DefaultConnection");
            if (!string.IsNullOrEmpty(connectionString))
            {
                var seedUser = new SeedUser(request.Name, request.Email, request.Password, IsSystemAdmin: true);
                await DatabaseSeeder.SetupInitialOrganizationAsync(connectionString, new[] { seedUser });
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "初回管理者は作成しましたが、既定ワークスペースの用意に失敗しました。");
        }

        return CreateInitialAdminResult.Success;
    }

    /// <summary>直列化失敗(40001)・一意制約違反(23505)なら競合として扱う。</summary>
    private static bool IsConflict(Exception ex)
    {
        for (var current = ex; current is not null; current = current.InnerException)
        {
            if (current is PostgresException pg
                && pg.SqlState is SerializationFailure or UniqueViolation)
            {
                return true;
            }
        }

        return false;
    }
}
