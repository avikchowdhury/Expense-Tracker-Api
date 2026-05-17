using System.ComponentModel.DataAnnotations;

namespace ExpenseTracker.Api.Dtos
{
    public class AuthRegisterOtpDto
    {
        [Required]
        [EmailAddress]
        [NotBlank]
        public string Email { get; set; } = null!;

        [Required]
        [NotBlank]
        public string Otp { get; set; } = null!;

        [Required]
        [NotBlank]
        [MinLength(6)]
        public string Password { get; set; } = null!;
    }
}
