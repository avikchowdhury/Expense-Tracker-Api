using System.ComponentModel.DataAnnotations;

namespace ExpenseTracker.Api.Models
{
    public class Budget
    {
        public int Id { get; set; }

        public int UserId { get; set; }

        public User? User { get; set; }

        [Required]
        public string Category { get; set; } = "General";

        [Required]
        public decimal MonthlyLimit { get; set; }

        public decimal CurrentSpent { get; set; }

        public DateTime LastReset { get; set; } = DateTime.UtcNow;
    }
}
