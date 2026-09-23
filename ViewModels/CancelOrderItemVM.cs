using System.ComponentModel.DataAnnotations;

namespace Kayane.ViewModels;

public class CancelOrderItemVM
{
    public Guid OrderItemId { get; set; }

    [Required(ErrorMessage = "Please provide a reason.")]
    [StringLength(500, MinimumLength = 3)]
    public string Reason { get; set; } = string.Empty;
}