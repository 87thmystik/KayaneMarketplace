using System.ComponentModel.DataAnnotations;

namespace Kayane.ViewModels;

public class CancelOrderVM
{
    public Guid OrderId { get; set; }
    public string ShortOrderId => OrderId.ToString()[..8].ToUpperInvariant();

    [Required(ErrorMessage = "Please tell us why you're cancelling.")]
    [StringLength(500, MinimumLength = 3)]
    public string Reason { get; set; } = string.Empty;
}