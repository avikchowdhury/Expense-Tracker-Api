namespace ExpenseTracker.Api.Dtos
{
    public class QuickAddReceiptDto
    {
        public string Vendor { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public string? Category { get; set; }
        public DateTime? Date { get; set; }
    }
}
