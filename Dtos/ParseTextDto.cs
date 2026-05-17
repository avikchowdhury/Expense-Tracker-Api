using System.ComponentModel.DataAnnotations;

namespace ExpenseTracker.Api.Dtos
{
    public class ParseTextRequestDto
    {
        [Required]
        [NotBlank]
        public string Text { get; set; } = string.Empty;
    }

    public class ParseTextResultDto
    {
        public string Vendor { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public string Category { get; set; } = string.Empty;
        public string Date { get; set; } = string.Empty;
        public bool Parsed { get; set; }
        public string RawText { get; set; } = string.Empty;
    }
}
