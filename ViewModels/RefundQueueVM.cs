namespace Kayane.ViewModels;

public class RefundQueueVM
{
    public List<RefundQueueItemVM> Items { get; set; } = new();

    // Filter
    public string CurrentFilter { get; set; } = "pending";  // "pending" | "processed" | "all"

    // Counts
    public int PendingCount { get; set; }
    public int ProcessedCount { get; set; }

    // Total value pending
    public decimal PendingTotal { get; set; }
}