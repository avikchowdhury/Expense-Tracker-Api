namespace ExpenseTracker.Api.Dtos
{
    public class SpendingForecastDto
    {
        public decimal CurrentSpend { get; set; }
        public decimal ProjectedMonthEnd { get; set; }
        public decimal DailyAverage { get; set; }
        public int DaysElapsed { get; set; }
        public int DaysRemaining { get; set; }
        public string Trend { get; set; } = "on-track"; // on-track | warning | critical
        public string AiNarrative { get; set; } = string.Empty;
        public List<DailySpendPointDto> DailyBreakdown { get; set; } = new();
    }

    public class DailySpendPointDto
    {
        public string Date { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public bool IsProjected { get; set; }
    }
}
