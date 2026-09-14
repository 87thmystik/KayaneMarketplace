using System.ComponentModel.DataAnnotations;

namespace Kayane.Models;

public class Notification
{
    [Key]
    public Guid NotificationId { get; set; } = Guid.NewGuid();

    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    [Required]
    [MaxLength(250)]
    public string Title { get; set; } = string.Empty;

    [Required]
    public string Message { get; set; } = string.Empty;

    public NotificationType Type { get; set; } = NotificationType.General;

    public bool IsRead { get; set; } = false;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

