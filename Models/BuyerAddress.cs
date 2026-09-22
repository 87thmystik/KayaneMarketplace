using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Kayane.Models;

[Table("buyer_addresses")]
public class BuyerAddress
{
    [Key]
    [Column("address_id")]
    public Guid AddressId { get; set; } = Guid.NewGuid();

    [Column("user_id")]
    public Guid UserId { get; set; }

    [ForeignKey(nameof(UserId))]
    public User User { get; set; } = null!;

    [Column("label"), MaxLength(50)]
    public string Label { get; set; } = "Home";

    [Column("recipient_name"), MaxLength(200)]
    public string RecipientName { get; set; } = string.Empty;

    [Column("phone"), MaxLength(30)]
    public string Phone { get; set; } = string.Empty;

    [Column("address_line_1"), MaxLength(300)]
    public string AddressLine1 { get; set; } = string.Empty;

    [Column("address_line_2"), MaxLength(300)]
    public string? AddressLine2 { get; set; }

    [Column("city"), MaxLength(100)]
    public string City { get; set; } = string.Empty;

    [Column("state"), MaxLength(100)]
    public string State { get; set; } = string.Empty;

    [Column("is_default")]
    public bool IsDefault { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [NotMapped]
    public string FullAddress
    {
        get
        {
            var parts = new[] { AddressLine1, AddressLine2, City, State }
                .Where(p => !string.IsNullOrWhiteSpace(p));
            return string.Join(", ", parts);
        }
    }
}