using System.ComponentModel.DataAnnotations;

namespace ExpenseTracker.Api.Dtos
{
    public class ForgotPasswordDto
    {
        [Required]
        [EmailAddress]
        [NotBlank]
        public string Email { get; set; } = string.Empty;
    }
}
