namespace ExpenseTracker.Api.Dtos
{
    public class BudgetAdvisorSnapshotDto
    {
        public DateTime GeneratedAt { get; set; }
        public decimal TotalBudget { get; set; }
        public decimal CurrentSpend { get; set; }
        public decimal ProjectedSpend { get; set; }
        public decimal SuggestedBudget { get; set; }
        public decimal RemainingBudget { get; set; }
        public int DaysElapsed { get; set; }
        public int DaysRemaining { get; set; }
        public int DaysInMonth { get; set; }
        public string PaceStatus { get; set; } = "info";
        public string Summary { get; set; } = string.Empty;
        public List<string> Recommendations { get; set; } = new();
        public List<BudgetAdvisorCategoryDto> Categories { get; set; } = new();
    }

    public class BudgetAdvisorCategoryDto
    {
        public string Category { get; set; } = string.Empty;
        public decimal Budget { get; set; }
        public decimal Spent { get; set; }
        public decimal ProjectedSpend { get; set; }
        public decimal SuggestedBudget { get; set; }
        public decimal HistoricalAverage { get; set; }
        public decimal Remaining { get; set; }
        public string RiskLevel { get; set; } = "info";
        public string Insight { get; set; } = string.Empty;
    }
}
