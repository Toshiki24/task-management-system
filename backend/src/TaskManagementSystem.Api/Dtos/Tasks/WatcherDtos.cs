namespace TaskManagementSystem.Api.Dtos.Tasks;

/// <summary>ウォッチャー 1 人(M3 §4)。</summary>
public record WatcherDto(long UserId, string Name);

/// <summary>タスクのウォッチャー一覧＋自分がウォッチ中かどうか(M3 §4)。</summary>
public record WatchersDto(IReadOnlyList<WatcherDto> Watchers, bool Watching);
