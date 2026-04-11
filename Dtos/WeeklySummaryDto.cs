namespace ExpenseTracker.Api.Dtos
{
    public class WeeklySummaryDto
    {
        public string RangeLabel { get; set; } = string.Empty;
        public decimal TotalSpend { get; set; }
        public int ReceiptCount { get; set; }
        public string TopCategory { get; set; } = "N/A";
        public string ForecastRisk { get; set; } = "On track";
        public string Recommendation { get; set; } = string.Empty;
        public List<WeeklyCategorySpendDto> TopCategories { get; set; } = new();
    }

    public class WeeklyCategorySpendDto
    {
        public string Category { get; set; } = string.Empty;
        public decimal TotalSpend { get; set; }
    }
}
