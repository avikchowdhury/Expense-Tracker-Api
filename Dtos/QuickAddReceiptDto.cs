using ExpenseTracker.Shared.Constants;
using System.ComponentModel.DataAnnotations;

namespace ExpenseTracker.Api.Dtos
{
    public class QuickAddReceiptDto
    {
        [Required]
        [NotBlank(ErrorMessage = ApplicationText.Validation.VendorRequired)]
        [StringLength(200)]
        public string Vendor { get; set; } = string.Empty;

        [Range(typeof(decimal), "0.01", "79228162514264337593543950335", ErrorMessage = ApplicationText.Validation.PositiveAmountRequired)]
        public decimal Amount { get; set; }

        [StringLength(100)]
        public string? Category { get; set; }
        public DateTime? Date { get; set; }
    }
}
