using System.ComponentModel.DataAnnotations;

namespace ExpenseTracker.Api.Dtos
{
    public class DuplicateCheckRequestDto
    {
        [Required]
        [NotBlank]
        public string Vendor { get; set; } = string.Empty;

        [Range(typeof(decimal), "0.01", "79228162514264337593543950335")]
        public decimal Amount { get; set; }

        [Required]
        [NotBlank]
        public string Date { get; set; } = string.Empty;
    }

    public class DuplicateCheckResultDto
    {
        public bool IsDuplicate { get; set; }
        public string Warning { get; set; } = string.Empty;
        public List<ReceiptMatchDto> PotentialMatches { get; set; } = new();
    }

    public class ReceiptMatchDto
    {
        public int Id { get; set; }
        public string Vendor { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public string Date { get; set; } = string.Empty;
        public string MatchReason { get; set; } = string.Empty;
    }
}
