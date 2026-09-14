using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Kayane.Models;

public class VendorWallet
{
    [Key]
    public Guid WalletId { get; set; } = Guid.NewGuid();

    public Guid VendorId { get; set; }
    public Vendor Vendor { get; set; } = null!;

    [Column(TypeName = "decimal(18,2)")]
    public decimal Balance { get; set; } = 0.00m;

    [Column(TypeName = "decimal(18,2)")]
    public decimal TotalEarned { get; set; } = 0.00m;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}