// Models/AdminAction.cs
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Kayane.Models;

[Table("admin_actions")]
public class AdminAction
{
    [Key]
    [Column("action_id")]
    public Guid ActionId { get; set; } = Guid.NewGuid();

    [Column("admin_id")]
    public Guid AdminId { get; set; }

    [Required, Column("action_type")]
    public string ActionType { get; set; } = string.Empty;

    [Required, Column("target_type")]
    public string TargetType { get; set; } = string.Empty;

    [Column("target_id")]
    public Guid TargetId { get; set; }

    [Column("details", TypeName = "jsonb")]
    public string? Details { get; set; }

    [Column("timestamp")]
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}