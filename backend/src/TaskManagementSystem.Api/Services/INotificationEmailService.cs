using TaskManagementSystem.Api.Models;

namespace TaskManagementSystem.Api.Services;

/// <summary>
/// アプリ内通知に対応するメール通知の送信(Phase 2 M5 §8)。
/// 生成済みの <see cref="Notification"/> を受け取り、メール通知が有効な受信者にだけ送信する。
/// 送信はベストエフォートで、失敗しても呼び出し元の処理(通知保存)には影響させない。
/// </summary>
public interface INotificationEmailService
{
    /// <summary>
    /// 生成した通知に対応するメールを送信する。
    /// 受信者ごとに <see cref="User.EmailNotificationsEnabled"/> を確認し、無効なら送信しない。
    /// DB への保存(SaveChanges/コミット)が完了したあとに呼ぶこと。
    /// </summary>
    Task SendAsync(IReadOnlyCollection<Notification> notifications, CancellationToken cancellationToken = default);
}

/// <summary>
/// 何もしないメール通知サービス(Null Object)。
/// メール通知が本質でないユニットテストで、サービスを素の DbContext だけで生成できるようにするための既定値。
/// 本番では DI が <see cref="NotificationEmailService"/> を注入する。
/// </summary>
public sealed class NullNotificationEmailService : INotificationEmailService
{
    public static readonly NullNotificationEmailService Instance = new();

    public Task SendAsync(
        IReadOnlyCollection<Notification> notifications, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
}
