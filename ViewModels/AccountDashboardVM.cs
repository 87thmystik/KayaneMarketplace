namespace Kayane.ViewModels;

public class AccountDashboardVM
{
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public DateTime MemberSince { get; set; }

    public int TotalOrders { get; set; }
    public int ActiveOrders { get; set; }
    public int DeliveredOrders { get; set; }
    public int AddressCount { get; set; }
    public int ReviewCount { get; set; }
    public int UnreadNotifications { get; set; }

    public List<RecentOrderItemVM> RecentOrders { get; set; } = new();

    public class RecentOrderItemVM
    {
        public Guid OrderId { get; set; }
        public DateTime CreatedAt { get; set; }
        public decimal TotalAmount { get; set; }
        public string Status { get; set; } = string.Empty;
        public int ItemCount { get; set; }
    }
}