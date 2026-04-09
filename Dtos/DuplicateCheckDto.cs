namespace ExpenseTracker.Api.Dtos
{
    public class DuplicateCheckRequestDto
    {
        public string Vendor { get; set; } = string.Empty;
        public decimal Amount { get; set; }
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
