using Kayane.Models;

namespace Kayane.ViewModels
{
    public class VendorModerationVM
    {
        public Guid VendorId { get; set; }
        public string BusinessName { get; set; } = string.Empty;
        public string OwnerName { get; set; } = string.Empty;
        public string OwnerEmail { get; set; } = string.Empty;
        public VendorStatus Status { get; set; }
        public DateTime CreatedAt { get; set; }
        public string? Phone { get; set; }
    }
}
