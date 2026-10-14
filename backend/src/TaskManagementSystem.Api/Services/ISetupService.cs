using TaskManagementSystem.Api.Dtos.Setup;

namespace TaskManagementSystem.Api.Services;

/// <summary>初回セットアップ状態の確認結果。DB 障害を「未セットアップ」と誤認しないよう区別する。</summary>
public enum SetupStatusResult { Ok, Disabled, DbError }

/// <summary>初回管理者作成の結果。</summary>
public enum CreateInitialAdminResult { Success, Disabled, AlreadyInitialized, DbError }

public record SetupStatusOutcome(SetupStatusResult Result, SetupStatusDto? Data = null);

public interface ISetupService
{
    /// <summary>初回セットアップが必要かを返す。機能無効なら Disabled、DB 障害なら DbError。</summary>
    Task<SetupStatusOutcome> GetStatusAsync();

    /// <summary>
    /// 初回 System Admin を作成する。ユーザーが1人も居ないときだけ成功する(原子的)。
    /// 機能無効なら Disabled、既にユーザーがあれば AlreadyInitialized、DB 障害/競合なら DbError。
    /// </summary>
    Task<CreateInitialAdminResult> CreateInitialAdminAsync(CreateInitialAdminRequest request);
}
