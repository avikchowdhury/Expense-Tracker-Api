using System.ComponentModel.DataAnnotations;

namespace ExpenseTracker.Api.Models
{
    public class Expense
    {
        public int Id { get; set; }

        public int UserId { get; set; }
        public User? User { get; set; }

        public int? ReceiptId { get; set; }
        public Receipt? Receipt { get; set; }

        [Required]
        public DateTime Date { get; set; } = DateTime.UtcNow;

        [Required]
        public decimal Amount { get; set; }

        // New: CategoryId and navigation property
        public int? CategoryId { get; set; }
        public Category? Category { get; set; }

        public string? Description { get; set; }

        public string Currency { get; set; } = "USD";
    }
}
