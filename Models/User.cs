using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;


namespace Kayane.Models;
[Table("users")]
public class  User
{
    [Key]
    [Column("user_id")]
    public Guid UserId { get; set; } = Guid.NewGuid();

    [Required, Column("name")]
    public string Name { get; set; } = string.Empty;

    [Required, Column("email")]
    public string Email { get; set; } = string.Empty;

    [Required, Column("password_hash")]
    public string PasswordHash { get; set; } = string.Empty;

    [Column("role")]
    public UserRole Role { get; set; } = UserRole.User;

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    [Column("is_banned")]
    public bool IsBanned { get; set; }

    [Column("banned_reason")]
    public string? BannedReason { get; set; }

    [Column("must_change_password")]
    public bool MustChangePassword { get; set; }

    [Column("deleted_at")]
    public DateTime? DeletedAt { get; set; }

    public Vendor? VendorProfile { get; set; }
    public string Phone { get; set; } = string.Empty;
    [Column("reset_token_hash")]
    public string? ResetTokenHash { get; set; }

    [Column("reset_token_expires_at")]
    public DateTime? ResetTokenExpiresAt { get; set; }
}