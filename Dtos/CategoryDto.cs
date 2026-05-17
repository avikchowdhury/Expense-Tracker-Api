using ExpenseTracker.Shared.Constants;
using System.ComponentModel.DataAnnotations;

namespace ExpenseTracker.Api.Dtos
{
    public class CategoryDto
    {
        public int Id { get; set; }

        [Required]
        [NotBlank(ErrorMessage = ApplicationText.Validation.CategoryNameRequired)]
        [StringLength(100)]
        public string Name { get; set; } = string.Empty;
    }
}
