using System.ComponentModel.DataAnnotations;

namespace Kayane.ViewModels;

public class SubmitReviewVM
{
    public Guid ProductId { get; set; }

    [Range(1, 5, ErrorMessage = "Please select a rating between 1 and 5 stars.")]
    public int Rating { get; set; }

    [Required(ErrorMessage = "Please write a comment.")]
    [StringLength(1000, MinimumLength = 3)]
    public string Comment { get; set; } = string.Empty;
}