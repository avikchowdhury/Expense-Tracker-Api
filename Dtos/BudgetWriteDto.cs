using System.ComponentModel.DataAnnotations;
using ExpenseTracker.Shared.Constants;

namespace ExpenseTracker.Api.Dtos;

public sealed class BudgetWriteDto
{
    [Required]
    [NotBlank]
    [StringLength(100)]
    public string Category { get; set; } = ApplicationText.Defaults.GeneralCategory;

    [Range(typeof(decimal), "0.01", "79228162514264337593543950335")]
    public decimal MonthlyLimit { get; set; }
}
