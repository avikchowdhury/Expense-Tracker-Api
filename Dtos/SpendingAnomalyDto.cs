namespace ExpenseTracker.Api.Dtos
{
    public class SpendingAnomalyDto
    {
        public string Category { get; set; } = string.Empty;
        public decimal ThisMonth { get; set; }
        public decimal AverageMonth { get; set; }
        public decimal PercentageIncrease { get; set; }
        public string Severity { get; set; } = "normal"; // normal | warning | critical
        public string Message { get; set; } = string.Empty;
    }

    public class MonthlySummaryDto
    {
        public string Month { get; set; } = string.Empty;
        public decimal TotalSpend { get; set; }
        public string TopCategory { get; set; } = string.Empty;
        public int ReceiptCount { get; set; }
        public string AiSummary { get; set; } = string.Empty;
        public List<SpendingAnomalyDto> Anomalies { get; set; } = new();
    }
}
