namespace TaskManagementSystem.Api.Services;

/// <summary>
/// 期限が近い/過ぎている未完タスクについて通知を生成する(M3 §6/§7。スケジュール実行)。
/// </summary>
public interface IDueNotificationService
{
    /// <summary>
    /// 指定日を基準に DUE_SOON / DUE_OVERDUE 通知を生成する。生成した通知の件数を返す。
    /// 同一日(today 00:00 以降)に同じ (受信者, タスク, 種別) の通知が既にあれば作らない(重複防止)。
    /// </summary>
    Task<int> GenerateAsync(DateOnly today, int dueSoonWithinDays);
}
