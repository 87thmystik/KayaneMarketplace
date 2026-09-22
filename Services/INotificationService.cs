using Kayane.Models;

namespace Kayane.Services;

public interface INotificationService
{
    /// <summary>
    /// Queues a notification. Does NOT call SaveChanges — the caller
    /// must save so the notification lands atomically with the change.
    /// </summary>
    Task NotifyAsync(Guid userId, string title, string message, NotificationType type);

    /// <summary>
    /// Queues the same notification to multiple users in one batch.
    /// Does NOT call SaveChanges.
    /// </summary>
    Task NotifyManyAsync(IEnumerable<Guid> userIds, string title, string message, NotificationType type);
}