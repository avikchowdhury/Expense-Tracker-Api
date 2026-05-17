using System.ComponentModel.DataAnnotations;

namespace ExpenseTracker.Api.Dtos;

public sealed class OtpEmailRequestDto
{
    [Required]
    [EmailAddress]
    [NotBlank]
    public string Email { get; set; } = string.Empty;
}
