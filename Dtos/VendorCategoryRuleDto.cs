using ExpenseTracker.Shared.Constants;
using System.ComponentModel.DataAnnotations;

namespace ExpenseTracker.Api.Dtos
{
    public class VendorCategoryRuleDto
    {
        public int Id { get; set; }

        [Range(1, int.MaxValue, ErrorMessage = ApplicationText.Validation.SelectValidCategory)]
        public int CategoryId { get; set; }
        public string CategoryName { get; set; } = string.Empty;

        [Required]
        [NotBlank(ErrorMessage = ApplicationText.Validation.VendorPatternRequired)]
        [StringLength(200)]
        public string VendorPattern { get; set; } = string.Empty;
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; }
    }
}
