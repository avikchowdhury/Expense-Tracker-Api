using ExpenseTracker.Shared.Constants;
using System.ComponentModel.DataAnnotations;

namespace ExpenseTracker.Api.Dtos
{
    public class UpdateProfileDto
    {
        [EmailAddress(ErrorMessage = ApplicationText.Auth.EnterValidEmailAddress)]
        [StringLength(256)]
        public string? Email { get; set; }

        [StringLength(120)]
        public string? FullName { get; set; }

        [StringLength(20)]
        public string? Phone { get; set; }

        [StringLength(400)]
        public string? Address { get; set; }
        public bool? BudgetNotificationsEnabled { get; set; }
        public bool? AnomalyNotificationsEnabled { get; set; }
        public bool? SubscriptionNotificationsEnabled { get; set; }
        public bool? WeeklySummaryEmailEnabled { get; set; }
        public bool? MonthlyReportEmailEnabled { get; set; }

        [StringLength(20)]
        public string? WeeklySummaryDay { get; set; }
    }
}
