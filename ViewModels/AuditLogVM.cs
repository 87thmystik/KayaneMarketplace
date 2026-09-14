public class AuditLogVM
{
    public string ActionType { get; set; } = string.Empty;
    public string TargetType { get; set; } = string.Empty;
    public string TargetId { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
}