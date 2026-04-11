namespace ExpenseTracker.Api.Dtos
{
    public class WhatIfForecastRequestDto
    {
        public List<ForecastAdjustmentDto> Adjustments { get; set; } = new();
    }

    public class ForecastAdjustmentDto
    {
        public string Category { get; set; } = string.Empty;
        public decimal DeltaAmount { get; set; }
    }

    public class WhatIfForecastDto
    {
        public decimal BaseProjectedMonthEnd { get; set; }
        public decimal AdjustedProjectedMonthEnd { get; set; }
        public decimal NetChange { get; set; }
        public string Trend { get; set; } = "on-track";
        public string Summary { get; set; } = string.Empty;
        public List<ForecastAdjustmentDto> Adjustments { get; set; } = new();
    }

    public class ForecastDriverDto
    {
        public string Category { get; set; } = string.Empty;
        public decimal Amount { get; set; }
    }
}
