using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Kayane.Models;

[Table("vendors")]
public class  Vendor
{
    [Key]
    [Column("vendor_id")]
    public Guid VendorId { get; set; } = Guid.NewGuid();

    [Column("user_id")]
    public Guid UserId { get; set; }

    [Required, Column("business_name")]
    public string BusinessName { get; set; } = string.Empty;

    [Column("contact_info", TypeName = "jsonb")]
    public string? ContactInfo { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [ForeignKey(nameof(UserId))]
    public User User { get; set; } = null!;

    public ICollection<Product> Products { get; set; } = new List<Product>();
    public VendorWallet Wallet { get; set; } = new();

    public string? BusinessDescription { get; set; }
    public string? BusinessAddress { get; set; }
    public string? BusinessPhone { get; set; }
    public string SupportPhone { get; set; } = string.Empty;
    public string? SupportEmail { get; set; }
    public string? BankName { get; set; }
    public required string AccountNumber { get; set; }
    public string AccountName { get; set; } = string.Empty;
    public string BankCode { get; set; } = string.Empty;
    public string LogoUrl { get; set; } = string.Empty;
    public string? BannerUrl { get; set; }

    public string Slug { get; set; } = string.Empty;
    public VendorStatus Status { get; set; } = VendorStatus.Pending;
}