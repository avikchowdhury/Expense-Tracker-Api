using System.ComponentModel.DataAnnotations;

namespace ExpenseTracker.Api.Dtos;

public sealed class BudgetWriteDto
{
    [Required]
    [NotBlank]
    [StringLength(100)]
    public string Category { get; set; } = "General";

    [Range(typeof(decimal), "0.01", "79228162514264337593543950335")]
    public decimal MonthlyLimit { get; set; }
}
