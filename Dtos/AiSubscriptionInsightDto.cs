namespace ExpenseTracker.Api.Dtos
{
    public class AiSubscriptionInsightDto
    {
        public string Vendor { get; set; } = string.Empty;
        public string Category { get; set; } = "Recurring";
        public decimal AverageAmount { get; set; }
        public decimal EstimatedMonthlyCost { get; set; }
        public string Frequency { get; set; } = "Recurring";
        public int Occurrences { get; set; }
        public DateTime? LastSeenAt { get; set; }
        public DateTime? NextExpectedDate { get; set; }
    }
}
