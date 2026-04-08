namespace ExpenseTracker.Api.Services
{
    public interface IBudgetHealthService
    {
        Task<BudgetHealthSnapshot> GetBudgetHealthAsync(int userId, DateTime periodStartUtc, DateTime periodEndUtc);
    }
}
