namespace Kayane.ViewModels
{
    public class AdminActionLogVM
    {
        public string ActionType { get; set; } = string.Empty;
        public string TargetType { get; set; } = string.Empty;
        public Guid TargetId { get; set; }
        public DateTime Timestamp { get; set; }
    }
}
