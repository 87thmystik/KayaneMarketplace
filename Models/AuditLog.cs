namespace Kayane.Models
{
    public class AuditLog
    {
        public Guid AuditLogId { get; set; } = Guid.NewGuid();
        public string Action { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public Guid UserId { get; set; } = Guid.NewGuid();
    }
}
