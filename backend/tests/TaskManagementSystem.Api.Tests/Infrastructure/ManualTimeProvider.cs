namespace TaskManagementSystem.Api.Tests.Infrastructure;

/// <summary>
/// テストから現在時刻を進められるTimeProvider。
/// 「一定時間が経過したら制限が解除される」といった時間に依存する処理を、実際に待たずに検証する。
/// </summary>
public sealed class ManualTimeProvider : TimeProvider
{
    private DateTimeOffset _utcNow = new(2026, 10, 1, 0, 0, 0, TimeSpan.Zero);

    public override DateTimeOffset GetUtcNow() => _utcNow;

    public void Advance(TimeSpan duration) => _utcNow += duration;
}
