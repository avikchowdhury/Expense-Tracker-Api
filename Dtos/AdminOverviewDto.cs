namespace ExpenseTracker.Api.Dtos
{
    public sealed class AdminOverviewDto
    {
        public int TotalUsers { get; set; }
        public int AdminCount { get; set; }
        public int StandardUserCount { get; set; }
        public int ReceiptCount { get; set; }
        public decimal TrackedReceiptSpend { get; set; }
    }
}
