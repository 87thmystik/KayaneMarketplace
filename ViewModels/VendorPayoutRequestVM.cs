using System.ComponentModel.DataAnnotations;

namespace Kayane.ViewModels
{
    public class VendorPayoutRequestVM
    {
        [Range(100, 10000000, ErrorMessage = "Please enter a valid payout amount.")]
        public decimal Amount { get; set; }
    }
}
