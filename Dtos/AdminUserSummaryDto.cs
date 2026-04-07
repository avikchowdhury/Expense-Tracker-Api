namespace ExpenseTracker.Api.Dtos
{
    public sealed class AdminUserSummaryDto
    {
        public int Id { get; set; }
        public string Email { get; set; } = string.Empty;
        public string Role { get; set; } = "User";
        public string? AvatarUrl { get; set; }
        public int ReceiptCount { get; set; }
        public int BudgetCount { get; set; }
        public int CategoryCount { get; set; }
        public DateTime? LatestReceiptAt { get; set; }
    }
}
