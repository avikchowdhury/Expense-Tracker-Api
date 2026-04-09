namespace ExpenseTracker.Api.Dtos
{
    public class VendorSummaryDto
    {
        public string Vendor { get; set; } = string.Empty;
        public decimal TotalSpend { get; set; }
        public int VisitCount { get; set; }
        public decimal AverageTransaction { get; set; }
        public decimal? ChangePercent { get; set; }
        public string Trend { get; set; } = "steady"; // up | down | steady | new
    }

    public class VendorAnalysisDto
    {
        public string Month { get; set; } = string.Empty;
        public List<VendorSummaryDto> TopVendors { get; set; } = new();
        public string AiObservation { get; set; } = string.Empty;
    }
}
