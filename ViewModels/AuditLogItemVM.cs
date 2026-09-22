namespace Kayane.ViewModels
{
    public class AuditLogItemVM
    {
        public Guid ActionId { get; set; }
        public DateTime Timestamp { get; set; }
        public Guid AdminId { get; set; }
        public string AdminName { get; set; } = "Unknown Admin";
        public string AdminEmail { get; set; } = string.Empty;
        public string ActionType { get; set; } = string.Empty;
        public string TargetType { get; set; } = string.Empty;
        public Guid TargetId { get; set; }
        public string? Details { get; set; }

        // Derived — populated by the controller
        public string ShortTargetId => TargetId.ToString()[..8];
    }

    public class AuditLogPageVM
    {
        public List<AuditLogItemVM> Items { get; set; } = new();
        public List<string> AvailableActionTypes { get; set; } = new();

        // Filters (echoed back to the view)
        public string? SelectedActionType { get; set; }
        public string? SearchTargetId { get; set; }
        public int DaysBack { get; set; } = 7;

        // Pagination
        public int CurrentPage { get; set; } = 1;
        public int PageSize { get; set; } = 50;
        public int TotalItems { get; set; }
        public int TotalPages => PageSize > 0
            ? (int)Math.Ceiling(TotalItems / (double)PageSize)
            : 1;
        public bool HasPreviousPage => CurrentPage > 1;
        public bool HasNextPage => CurrentPage < TotalPages;
    }
}