namespace ExpenseTracker.Api.Dtos
{
    public class UpdateProfileDto
    {
        public string? Email { get; set; }
        public string? FullName { get; set; }
        public string? Phone { get; set; }
        public string? Address { get; set; }
        public bool? BudgetNotificationsEnabled { get; set; }
        public bool? AnomalyNotificationsEnabled { get; set; }
        public bool? SubscriptionNotificationsEnabled { get; set; }
        public bool? WeeklySummaryEmailEnabled { get; set; }
        public bool? MonthlyReportEmailEnabled { get; set; }
        public string? WeeklySummaryDay { get; set; }
    }
}
