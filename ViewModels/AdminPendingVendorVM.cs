namespace Kayane.ViewModels
{
    public class AdminPendingVendorVM
    {
        public Guid VendorId { get; set; }
        public string BusinessName { get; set; } = string.Empty;
        public string BusinessPhone { get; set; } = string.Empty;
        public string BusinessEmail { get; set; } = string.Empty;
        public string BusinessAddress { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public string? LogoUrl { get; set; }
    }
}
