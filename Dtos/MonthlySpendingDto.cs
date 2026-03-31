namespace ExpenseTracker.Api.Dtos
{
    public class MonthlySpendingDto
    {
        public string Month { get; set; } = null!;
        public decimal Total { get; set; }
    }
}
