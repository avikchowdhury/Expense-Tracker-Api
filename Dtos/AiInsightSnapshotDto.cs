namespace ExpenseTracker.Api.Dtos
{
    public class AiInsightSnapshotDto
    {
        public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
        public string BudgetHealth { get; set; } = "Learning";
        public string EvidenceSummary { get; set; } = string.Empty;
        public decimal MonthSpend { get; set; }
        public decimal RecentAverage { get; set; }
        public string TopCategory { get; set; } = "N/A";
        public List<string> Anomalies { get; set; } = new();
        public List<string> Suggestions { get; set; } = new();
        public List<AiInsightDto> Insights { get; set; } = new();
        public List<AiCopilotAlertDto> Alerts { get; set; } = new();
        public List<AiSubscriptionInsightDto> Subscriptions { get; set; } = new();
    }
}
