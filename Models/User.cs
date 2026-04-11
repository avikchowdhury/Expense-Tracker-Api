using System.ComponentModel.DataAnnotations;

namespace ExpenseTracker.Api.Models
{
    public class User
    {
        public int Id { get; set; }

        [Required]
        [EmailAddress]
        public string Email { get; set; } = null!;

        [Required]
        public string PasswordHash { get; set; } = null!;

        public string Role { get; set; } = "User";

        public ICollection<Receipt> Receipts { get; set; } = new List<Receipt>();

        public ICollection<Expense> Expenses { get; set; } = new List<Expense>();

        public ICollection<Budget> Budgets { get; set; } = new List<Budget>();

        // Avatar/profile picture URL
        public string? AvatarUrl { get; set; }

        // Additional profile details
        public string? FullName { get; set; }
        public string? Phone { get; set; }
        public string? Address { get; set; }

        public bool BudgetNotificationsEnabled { get; set; } = true;

        public bool AnomalyNotificationsEnabled { get; set; } = true;

        public bool SubscriptionNotificationsEnabled { get; set; } = true;

        public bool WeeklySummaryEmailEnabled { get; set; } = true;

        public bool MonthlyReportEmailEnabled { get; set; }

        [MaxLength(20)]
        public string WeeklySummaryDay { get; set; } = "Monday";
    }
}
