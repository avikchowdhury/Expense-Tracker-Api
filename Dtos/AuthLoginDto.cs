using System.ComponentModel.DataAnnotations;

namespace ExpenseTracker.Api.Dtos
{
    public class AuthLoginDto
    {
        [Required]
        [EmailAddress]
        [NotBlank]
        public string Email { get; set; } = null!;

        [Required]
        [NotBlank]
        public string Password { get; set; } = null!;
    }
}
