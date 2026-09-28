using Kayane.Models;

namespace Kayane.ViewModels;

public class AdminPayoutListVM
{
    public List<PayoutTransaction> Payouts { get; set; } = new();

    public int CurrentPage { get; set; } = 1;
    public int PageSize { get; set; } = 25;
    public int TotalItems { get; set; }
    public int TotalPages => PageSize > 0
        ? (int)Math.Ceiling(TotalItems / (double)PageSize)
        : 1;
    public bool HasPreviousPage => CurrentPage > 1;
    public bool HasNextPage => CurrentPage < TotalPages;

    // Status counts for filter tabs (optional)
    public int PendingCount { get; set; }
    public int ApprovedCount { get; set; }
    public int RejectedCount { get; set; }
}