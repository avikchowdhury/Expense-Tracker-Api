namespace ExpenseTracker.Api.Dtos
{
    public class BudgetDto
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public string Category { get; set; } = "General";
        public decimal MonthlyLimit { get; set; }
        public decimal CurrentSpent { get; set; }
        public DateTime LastReset { get; set; }
    }
}
