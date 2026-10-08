using TaskManagementSystem.Api.Dtos.Tasks;

namespace TaskManagementSystem.Api.Services;

public enum WatchResult
{
    Success,
    TaskNotFound,
}

public interface IWatcherService
{
    /// <summary>ウォッチャー一覧＋自分のウォッチ状態を返す。非所属は null(404 相当)。</summary>
    Task<WatchersDto?> GetWatchersAsync(long taskId, long currentUserId);

    /// <summary>自分をウォッチャーに追加する(冪等)。CanView で可。</summary>
    Task<WatchResult> WatchAsync(long taskId, long currentUserId);

    /// <summary>自分をウォッチャーから外す(冪等)。</summary>
    Task<WatchResult> UnwatchAsync(long taskId, long currentUserId);
}
