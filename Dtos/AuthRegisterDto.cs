using System.ComponentModel.DataAnnotations;

namespace ExpenseTracker.Api.Dtos
{
    public class AuthRegisterDto
    {
        [Required]
        [EmailAddress]
        [NotBlank]
        public string Email { get; set; } = null!;

        [Required]
        [NotBlank]
        [MinLength(6)]
        public string Password { get; set; } = null!;
    }
}
