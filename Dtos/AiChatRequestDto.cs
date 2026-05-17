using System.ComponentModel.DataAnnotations;

namespace ExpenseTracker.Api.Dtos
{
    public class AiChatRequestDto
    {
        [Required]
        [NotBlank]
        public string Message { get; set; } = string.Empty;
    }
}
