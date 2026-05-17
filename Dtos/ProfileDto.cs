namespace ExpenseTracker.Api.Dtos;

public sealed class ProfileDto
{
    public string Email { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
    public string? FullName { get; set; }
    public string? Phone { get; set; }
    public string? Address { get; set; }
    public bool BudgetNotificationsEnabled { get; set; }
    public bool AnomalyNotificationsEnabled { get; set; }
    public bool SubscriptionNotificationsEnabled { get; set; }
    public bool WeeklySummaryEmailEnabled { get; set; }
    public bool MonthlyReportEmailEnabled { get; set; }
    public string WeeklySummaryDay { get; set; } = string.Empty;
}
