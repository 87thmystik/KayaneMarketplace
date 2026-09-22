using Kayane.Data;
using Kayane.Models;

namespace Kayane.Services;

public class NotificationService : INotificationService
{
    private readonly KayaneDb _context;

    public NotificationService(KayaneDb context)
    {
        _context = context;
    }

    public Task NotifyAsync(Guid userId, string title, string message, NotificationType type)
    {
        _context.Notifications.Add(new Notification
        {
            NotificationId = Guid.NewGuid(),
            UserId = userId,
            Title = title,
            Message = message,
            Type = type,
            IsRead = false,
            CreatedAt = DateTime.UtcNow
        });

        return Task.CompletedTask;
    }

    public Task NotifyManyAsync(IEnumerable<Guid> userIds, string title, string message, NotificationType type)
    {
        foreach (var userId in userIds.Distinct())
        {
            _context.Notifications.Add(new Notification
            {
                NotificationId = Guid.NewGuid(),
                UserId = userId,
                Title = title,
                Message = message,
                Type = type,
                IsRead = false,
                CreatedAt = DateTime.UtcNow
            });
        }

        return Task.CompletedTask;
    }
}