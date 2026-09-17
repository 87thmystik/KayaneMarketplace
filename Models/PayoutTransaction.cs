using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Kayane.Models
{
    public class  PayoutTransaction
    {
        [Key]
        public Guid PayoutId { get; set; } = Guid.NewGuid();

        public Guid VendorId { get; set; }
        public Vendor Vendor { get; set; } = null!; 

        [Column(TypeName = "decimal(18,2)")]
        public decimal Amount { get; set; }

        public string Reference { get; set; } = string.Empty;
        public string Status { get; set; } = "Pending"; // Pending, Success, Failed
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
