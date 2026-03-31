namespace ExpenseTracker.Api.Dtos
{
    public class ReceiptCreateDto
    {
        public int UserId { get; set; }

        public string? Vendor { get; set; }

        public decimal TotalAmount { get; set; }

        public string? Category { get; set; }

        public string? ParsedContentJson { get; set; }
    }
}
