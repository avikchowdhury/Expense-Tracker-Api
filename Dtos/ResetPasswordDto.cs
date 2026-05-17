using System.ComponentModel.DataAnnotations;

namespace ExpenseTracker.Api.Dtos
{
    public class ResetPasswordDto
    {
        [Required]
        [EmailAddress]
        [NotBlank]
        public string Email { get; set; } = string.Empty;

        [Required]
        [NotBlank]
        public string Token { get; set; } = string.Empty;

        [Required]
        [NotBlank]
        [MinLength(6)]
        public string NewPassword { get; set; } = string.Empty;
    }
}
