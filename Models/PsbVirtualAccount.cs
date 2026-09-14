using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Kayane.Models;

public class PsbVirtualAccount
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid VendorId { get; set; }
    public Vendor Vendor { get; set; } = null!;

    public string AccountNumber { get; set; } = string.Empty;
    public string AccountName { get; set; } = string.Empty;
    public string BankName { get; set; } = "9 Payment Service Bank";
    public string BankCode { get; set; } = "120001";

    public string? Reference { get; set; }
    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}