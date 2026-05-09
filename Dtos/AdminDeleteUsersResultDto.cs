namespace ExpenseTracker.Api.Dtos
{
    public sealed class AdminDeleteUsersResultDto
    {
        public int RequestedCount { get; set; }
        public int DeletedCount { get; set; }
        public int DeletedReceiptCount { get; set; }
        public int DeletedExpenseCount { get; set; }
        public int DeletedBudgetCount { get; set; }
        public int DeletedCategoryCount { get; set; }
        public int DeletedVendorRuleCount { get; set; }
        public IReadOnlyCollection<int> DeletedUserIds { get; set; } = Array.Empty<int>();
        public string Message { get; set; } = string.Empty;
    }
}
