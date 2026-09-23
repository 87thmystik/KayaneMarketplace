namespace Kayane.ViewModels;

public class RefundQueueItemVM
{
    public Guid PaymentId { get; set; }
    public Guid OrderId { get; set; }
    public string PaymentReference { get; set; } = string.Empty;
    public decimal Amount { get; set; }

    public string BuyerName { get; set; } = string.Empty;
    public string BuyerEmail { get; set; } = string.Empty;

    public DateTime PaymentCreatedAt { get; set; }
    public DateTime? CancelledAt { get; set; }   // Order.UpdatedAt (last update was the cancellation)
    public string CancellationReason { get; set; } = string.Empty;

    public List<RefundVendorLineVM> Vendors { get; set; } = new();

    // Processed state
    public bool IsProcessed { get; set; }
    public DateTime? RefundedAt { get; set; }
    public string? RefundedByName { get; set; }
}

public class RefundVendorLineVM
{
    public string VendorName { get; set; } = string.Empty;
    public decimal Amount { get; set; }
}