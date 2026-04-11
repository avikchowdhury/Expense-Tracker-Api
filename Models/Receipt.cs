using System.ComponentModel.DataAnnotations;

namespace ExpenseTracker.Api.Models
{
    public class Receipt
    {
        public int Id { get; set; }

        public int UserId { get; set; }

        public User? User { get; set; }

        [Required]
        public DateTime UploadedAt { get; set; } = DateTime.UtcNow;

        [Required]
        public string FileName { get; set; } = null!;

        public string? BlobUrl { get; set; }

        public decimal TotalAmount { get; set; }

        public string? Vendor { get; set; }

        public string? Category { get; set; }

        public string? ParsedContentJson { get; set; }

        public bool IsMarkedDuplicate { get; set; }

        public ICollection<Expense> Expenses { get; set; } = new List<Expense>();
    }
}
