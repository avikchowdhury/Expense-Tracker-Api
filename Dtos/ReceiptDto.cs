namespace ExpenseTracker.Api.Dtos
{
    public class ReceiptDto
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public DateTime UploadedAt { get; set; }
        public string FileName { get; set; } = null!;
        public decimal TotalAmount { get; set; }
        public string? Vendor { get; set; }
        public string? Category { get; set; }
        public string? BlobUrl { get; set; }
        public string? ParsedContentJson { get; set; }
    }
}
