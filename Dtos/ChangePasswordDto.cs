using ExpenseTracker.Shared.Constants;
using System.ComponentModel.DataAnnotations;

namespace ExpenseTracker.Api.Dtos;

public sealed class ChangePasswordDto
{
    [Required]
    [NotBlank]
    public string OldPassword { get; set; } = string.Empty;

    [Required]
    [NotBlank]
    [MinLength(ApplicationText.Auth.MinimumPasswordLength, ErrorMessage = ApplicationText.Auth.PasswordMinimumLength)]
    public string NewPassword { get; set; } = string.Empty;
}
